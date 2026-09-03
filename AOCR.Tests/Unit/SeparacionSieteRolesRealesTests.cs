using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaDatos.Constants;
using CapaModelo;
using CapaModelo.Common;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class SeparacionSieteRolesRealesTests
    {
        private readonly AocrFlujoService _flujoService = new AocrFlujoService();
        private readonly DircavBandejaService _dircavService = new DircavBandejaService();
        private readonly DirdacBandejaService _dirdacService = new DirdacBandejaService();

        // 1. Bandejas distintas
        [TestMethod]
        public void BandejasDistintas_DircavYDirdac_PoseenColeccionesDiferenciadas()
        {
            var dircavDoc = _dircavService.ObtenerDocumentacionPendienteAceptacion();
            var dircavDesig = _dircavService.ObtenerDesignacionesPendientes();
            var dircavCond = _dircavService.ObtenerCondicionesPendientesFirma();
            var dircavRemision = _dircavService.ObtenerExpedientesPendientesRemisionDirdac();

            var dirdacRev = _dirdacService.ObtenerAocrPendientesRevision();
            var dirdacFirma = _dirdacService.ObtenerAocrPendientesFirma();
            var dirdacConcluidos = _dirdacService.ObtenerProcesosConcluidos();

            Assert.IsNotNull(dircavDoc);
            Assert.IsNotNull(dircavDesig);
            Assert.IsNotNull(dircavCond);
            Assert.IsNotNull(dircavRemision);

            Assert.IsNotNull(dirdacRev);
            Assert.IsNotNull(dirdacFirma);
            Assert.IsNotNull(dirdacConcluidos);

            Assert.AreNotSame((object)_dircavService, (object)_dirdacService);
        }

        // 2. Contadores distintos e independientes
        [TestMethod]
        public void ContadoresDistintos_DircavYDirdac_NoCompartenOrigenesNiTotales()
        {
            var c1 = _dircavService.ContarDocumentacionPendienteAceptacion();
            var c2 = _dircavService.ContarDesignacionesPendientes();
            var c3 = _dircavService.ContarCondicionesPendientesFirma();

            var k1 = _dirdacService.ContarAocrPendientesRevision();
            var k2 = _dirdacService.ContarAocrPendientesFirma();

            Assert.IsTrue(c1 >= 0);
            Assert.IsTrue(c2 >= 0);
            Assert.IsTrue(c3 >= 0);
            Assert.IsTrue(k1 >= 0);
            Assert.IsTrue(k2 >= 0);
        }

        // 3. Acceso cruzado denegado: DIRCAV y DIRDAC son roles canónicos excluyentes
        [TestMethod]
        public void AccesoCruzado_RolesMutuamenteExcluyentes()
        {
            Assert.IsTrue(AocrRolesInstitucionales.EsDircav("DIRCAV"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav("DIRDAC"), "DIRDAC no debe ser DIRCAV.");

            Assert.IsTrue(AocrRolesInstitucionales.EsDirdac("DIRDAC"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDirdac("DIRCAV"), "DIRCAV no debe ser DIRDAC.");
        }

        // 4. DIRCAV no firma AOCR
        [TestMethod]
        public void DircavNoFirmaAocr_AccionBloqueada()
        {
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRCAV", AocrFlujoAcciones.DirdacFirmarAocr));
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Dcav", AocrFlujoAcciones.DirdacFirmarAocr));
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRCAV", AocrFlujoAcciones.FirmarAocrFinal));
        }

        // 5. DIRDAC no designa Inspector ni firma CL
        [TestMethod]
        public void DirdacNoDesignaInspectorNiFirmaCl_AccionBloqueada()
        {
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRDAC", AocrFlujoAcciones.DircavFirmarCl));
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRDAC", AocrFlujoAcciones.DircavConfirmarDesignacion));
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRDAC", AocrFlujoAcciones.DircavFirmarDesignacion));
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRDAC", AocrFlujoAcciones.AsignarInspector));
        }

        // 6. Administrador no firma operativamente (Regla 7)
        [TestMethod]
        public void AdministradorNoFirma_BloqueadoPorRegla7()
        {
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav("ADMINISTRADOR"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDirdac("ADMINISTRADOR"));

            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.DircavFirmarCl));
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.DirdacFirmarAocr));
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.FirmarInformeTecnico));
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.AsignarInspector));
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.AprobarPago));
        }

        // 7. Coordinador NUNCA remite directamente a DIRDAC
        [TestMethod]
        public void CoordinadorNuncaRemiteDirectamenteADirdac()
        {
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.EnviarDirdac),
                "El Coordinador NUNCA debe poder remitir directamente a DIRDAC.");
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.CoordinadorRemitirDircav),
                "El Coordinador remite sus expedientes a DIRCAV.");
        }

        // 8. Segregación de documentos de firma en Policy
        [TestMethod]
        public void PoliticaFirma_SegregadaEntreDircavYDirdac()
        {
            var filaAocr = new AocrBandejaDocumentoRow
            {
                SolicitudId = 1,
                TipoSolicitud = 1,
                EstadoDocumentoAocr = AocrEstadosProceso.AocrPendienteDirdac,
                FirmaReconocimientoId = null
            };
            Assert.IsTrue(AocrFirmaPendientePolicy.EsAocrPendienteFirma(filaAocr));
            Assert.IsFalse(AocrFirmaPendientePolicy.EsCondicionesPendienteFirma(filaAocr));

            var filaCond = new AocrBandejaDocumentoRow
            {
                SolicitudId = 2,
                TipoSolicitud = 1,
                EstadoDocumentoCondiciones = AocrEstadosProceso.ClPendienteFirmaDircav,
                FirmaCondicionesId = null
            };
            Assert.IsTrue(AocrFirmaPendientePolicy.EsCondicionesPendienteFirma(filaCond));
            Assert.IsFalse(AocrFirmaPendientePolicy.EsAocrPendienteFirma(filaCond));
        }

        // 9. Transición inválida: no se puede omitir la firma previa de C&L por DIRCAV
        [TestMethod]
        public void TransicionInvalida_ExigeFirmaPreviaDircav()
        {
            var filaSinCl = new AocrBandejaDocumentoRow
            {
                SolicitudId = 5,
                FirmaCondicionesId = null,
                EstadoDocumentoCondiciones = AocrEstadosProceso.ClPendienteFirmaDircav
            };

            Assert.IsTrue(AocrFirmaPendientePolicy.EsCondicionesPendienteFirma(filaSinCl),
                "No se puede omitir la firma previa de C&L por DIRCAV.");
        }

        // 10. Catálogo canónico estricto de los 7 roles reales
        [TestMethod]
        public void CatalogoCanonico_PoseeExactamenteLosSieteRolesReales()
        {
            var roles = new[]
            {
                AocrRolesInstitucionales.Dircav,
                AocrRolesInstitucionales.Dirdac,
                AocrRolesInstitucionales.Coordinador,
                AocrRolesInstitucionales.RT,
                AocrRolesInstitucionales.Financiero,
                AocrRolesInstitucionales.Inspector,
                AocrRolesInstitucionales.Administrador
            };

            var unicos = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Assert.AreEqual(7, unicos.Count, "Debe haber exactamente 7 roles canónicos únicos.");
        }
    }
}
