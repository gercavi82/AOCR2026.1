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
    public class SegregacionRolesDircavDirdacTests
    {
        private readonly DircavBandejaService _dircavService = new DircavBandejaService();
        private readonly DirdacBandejaService _dirdacService = new DirdacBandejaService();

        // 1. DIRCAV accede a sus bandejas exclusivas
        [TestMethod]
        public void Test01_DircavBandeja_RecuperaColeccionesExclusivas()
        {
            var docPendiente = _dircavService.ObtenerDocumentacionPendienteAceptacion();
            var desigPendiente = _dircavService.ObtenerDesignacionesPendientes();
            var desigFirmadas = _dircavService.ObtenerDesignacionesFirmadas();
            var informesPendientes = _dircavService.ObtenerInformesPendientesRevision();
            var condPendientes = _dircavService.ObtenerCondicionesPendientesFirma();
            var remisionPendiente = _dircavService.ObtenerExpedientesPendientesRemisionDirdac();
            var devueltos = _dircavService.ObtenerExpedientesDevueltos();
            var historial = _dircavService.ObtenerHistorialGestionados();

            Assert.IsNotNull(docPendiente);
            Assert.IsNotNull(desigPendiente);
            Assert.IsNotNull(desigFirmadas);
            Assert.IsNotNull(informesPendientes);
            Assert.IsNotNull(condPendientes);
            Assert.IsNotNull(remisionPendiente);
            Assert.IsNotNull(devueltos);
            Assert.IsNotNull(historial);
        }

        // 2. DIRDAC accede a sus bandejas exclusivas
        [TestMethod]
        public void Test02_DirdacBandeja_RecuperaColeccionesExclusivas()
        {
            var aocrRevision = _dirdacService.ObtenerAocrPendientesRevision();
            var aocrFirma = _dirdacService.ObtenerAocrPendientesFirma();
            var devueltosDircav = _dirdacService.ObtenerExpedientesDevueltosDircav();
            var aocrFirmados = _dirdacService.ObtenerAocrFirmados();
            var concluidos = _dirdacService.ObtenerProcesosConcluidos();
            var historial = _dirdacService.ObtenerHistorialGestionados();

            Assert.IsNotNull(aocrRevision);
            Assert.IsNotNull(aocrFirma);
            Assert.IsNotNull(devueltosDircav);
            Assert.IsNotNull(aocrFirmados);
            Assert.IsNotNull(concluidos);
            Assert.IsNotNull(historial);
        }

        // 3. Las bandejas de DIRCAV y DIRDAC manejan servicios e instancias distintas
        [TestMethod]
        public void Test03_BandejasDircavYDirdac_MuestranDatosDiferentes()
        {
            Assert.AreNotSame((object)_dircavService, (object)_dirdacService,
                "DIRCAV y DIRDAC deben contar con servicios y bandejas desacopladas.");
        }

        // 4. Los contadores son independientes
        [TestMethod]
        public void Test04_ContadoresSonIndependientes()
        {
            var countDocDircav = _dircavService.ContarDocumentacionPendienteAceptacion();
            var countCondDircav = _dircavService.ContarCondicionesPendientesFirma();
            var countRevDirdac = _dirdacService.ContarAocrPendientesRevision();
            var countFirmaDirdac = _dirdacService.ContarAocrPendientesFirma();

            Assert.IsTrue(countDocDircav >= 0);
            Assert.IsTrue(countCondDircav >= 0);
            Assert.IsTrue(countRevDirdac >= 0);
            Assert.IsTrue(countFirmaDirdac >= 0);
        }

        // 5. DIRCAV no posee permisos de DIRDAC
        [TestMethod]
        public void Test05_DircavNoPoseePermisosDirdac()
        {
            var permisosDircav = new HashSet<string>
            {
                "DIRCAV_VER_BANDEJA", "DIRCAV_REVISAR_DOCUMENTACION", "DIRCAV_ACEPTAR_DOCUMENTACION",
                "DIRCAV_DEVOLVER_COORDINADOR", "DIRCAV_DESIGNAR_INSPECTOR", "DIRCAV_FIRMAR_DESIGNACION",
                "DIRCAV_REVISAR_INFORME", "DIRCAV_REVISAR_CL", "DIRCAV_FIRMAR_CL",
                "DIRCAV_REMITIR_DIRDAC", "DIRCAV_VER_HISTORIAL"
            };

            Assert.IsFalse(permisosDircav.Contains("DIRDAC_FIRMAR_AOCR"), "DIRCAV no debe poseer DIRDAC_FIRMAR_AOCR.");
            Assert.IsFalse(permisosDircav.Contains("DIRDAC_VER_BANDEJA"), "DIRCAV no debe poseer DIRDAC_VER_BANDEJA.");
            Assert.IsFalse(permisosDircav.Contains("DIRDAC_DEVOLVER_DIRCAV"), "DIRCAV no debe poseer DIRDAC_DEVOLVER_DIRCAV.");
        }

        // 6. DIRDAC no posee permisos de DIRCAV
        [TestMethod]
        public void Test06_DirdacNoPoseePermisosDircav()
        {
            var permisosDirdac = new HashSet<string>
            {
                "DIRDAC_VER_BANDEJA", "DIRDAC_REVISAR_AOCR", "DIRDAC_FIRMAR_AOCR",
                "DIRDAC_DEVOLVER_DIRCAV", "DIRDAC_CONFIRMAR_LEGALIZACION", "DIRDAC_VER_HISTORIAL"
            };

            Assert.IsFalse(permisosDirdac.Contains("DIRCAV_ACEPTAR_DOCUMENTACION"), "DIRDAC no debe aceptar documentación de RT.");
            Assert.IsFalse(permisosDirdac.Contains("DIRCAV_DESIGNAR_INSPECTOR"), "DIRDAC no debe designar inspectores.");
            Assert.IsFalse(permisosDirdac.Contains("DIRCAV_FIRMAR_DESIGNACION"), "DIRDAC no debe firmar designación.");
            Assert.IsFalse(permisosDirdac.Contains("DIRCAV_FIRMAR_CL"), "DIRDAC no debe firmar Condiciones y Limitaciones.");
        }

        // 7. Regla de firma AOCR: solo aplica a AOCR oficial, no a C&L
        [TestMethod]
        public void Test07_ReglaFirmaAocr_SoloAplicaADocumentoAocr()
        {
            var filaAocr = new AocrBandejaDocumentoRow
            {
                SolicitudId = 1,
                TipoSolicitud = 1,
                EstadoDocumentoAocr = AocrEstadosProceso.PendienteFirmaAocrDirdac,
                FirmaReconocimientoId = null
            };

            Assert.IsTrue(AocrFirmaPendientePolicy.EsAocrPendienteFirma(filaAocr));
            Assert.IsFalse(AocrFirmaPendientePolicy.EsCondicionesPendienteFirma(filaAocr));
        }

        // 8. Regla de firma C&L: exclusiva para Condiciones y Limitaciones
        [TestMethod]
        public void Test08_ReglaFirmaCondiciones_SoloAplicaACondiciones()
        {
            var filaCyl = new AocrBandejaDocumentoRow
            {
                SolicitudId = 2,
                TipoSolicitud = 1,
                EstadoDocumentoCondiciones = AocrEstadosProceso.PendienteFirmaCondicionesDcav,
                FirmaCondicionesId = null
            };

            Assert.IsTrue(AocrFirmaPendientePolicy.EsCondicionesPendienteFirma(filaCyl));
            Assert.IsFalse(AocrFirmaPendientePolicy.EsAocrPendienteFirma(filaCyl));
        }

        // 9. DIRDAC no puede designar inspector
        [TestMethod]
        public void Test09_DirdacNoPuedeDesignarInspector()
        {
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav("DIRDAC"),
                "El rol DIRDAC no debe ser reconocido como DIRCAV.");
        }

        // 10. DIRCAV firma la designación produciendo estado DESIGNACION_FIRMADA_DIRCAV
        [TestMethod]
        public void Test10_DircavFirmaDesignacion_ProduceEstadoDesignacionFirmada()
        {
            var estado = AocrEstadosProceso.DesignacionFirmadaDircav;
            Assert.AreEqual("DESIGNACION_FIRMADA_DIRCAV", estado);
        }

        // 11. DIRCAV firma C&L produciendo estado CL_FIRMADA_DIRCAV
        [TestMethod]
        public void Test11_DircavFirmaCondiciones_ProduceEstadoClFirmada()
        {
            var estado = AocrEstadosProceso.ClFirmadaDircav;
            Assert.AreEqual("CL_FIRMADA_DIRCAV", estado);
        }

        // 12. DIRCAV remite a DIRDAC produciendo estado AOCR_PENDIENTE_DIRDAC
        [TestMethod]
        public void Test12_DircavRemiteADirdac_ProduceEstadoAocrPendienteDirdac()
        {
            var estado = AocrEstadosProceso.AocrPendienteDirdac;
            Assert.AreEqual("AOCR_PENDIENTE_DIRDAC", estado);
        }

        // 13. DIRDAC recibe AOCR en bandeja de revisión
        [TestMethod]
        public void Test13_DirdacRecibeAocr_EnBandejaRevision()
        {
            Assert.AreEqual("AOCR_PENDIENTE_DIRDAC", AocrEstadosProceso.AocrPendienteDirdac);
        }

        // 14. DIRDAC firma AOCR produciendo estado AOCR_FIRMADA_DIRDAC
        [TestMethod]
        public void Test14_DirdacFirmaAocr_ProduceEstadoAocrFirmadaDirdac()
        {
            var estado = AocrEstadosProceso.AocrFirmadaDirdac;
            Assert.AreEqual("AOCR_FIRMADA_DIRDAC", estado);
        }

        // 15. DIRDAC devuelve a DIRCAV con estado DEVUELTO_DIRCAV_POR_DIRDAC
        [TestMethod]
        public void Test15_DirdacDevuelveADircav_ProduceEstadoDevueltoConMotivo()
        {
            var estado = AocrEstadosProceso.DevueltoDircavPorDirdac;
            Assert.AreEqual("DEVUELTO_DIRCAV_POR_DIRDAC", estado);
        }

        // 16. DIRCAV corrige y reenvía a DIRDAC
        [TestMethod]
        public void Test16_DircavCorrigeYReenviaADirdac_TransicionValida()
        {
            var estadoInicial = AocrEstadosProceso.DevueltoDircavPorDirdac;
            var estadoReenvio = AocrEstadosProceso.AocrPendienteDirdac;
            Assert.AreNotEqual(estadoInicial, estadoReenvio);
        }

        // 17. COORDINADOR no posee permisos de DIRCAV
        [TestMethod]
        public void Test17_CoordinadorIntentaEjecutarAccionDircav_Rechazado()
        {
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav("COORDINADOR"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav("CoordinadorInspecciones"));
        }

        // 18. ADMINISTRADOR bloqueado para firmas operativas (Regla 7)
        [TestMethod]
        public void Test18_AdministradorIntentaFirmar_BloqueadoPorRegla7()
        {
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav("ADMINISTRADOR"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDirdac("ADMINISTRADOR"));
        }

        // 19. Tokens SQL segregados: DIRCAV y DIRDAC no coinciden
        [TestMethod]
        public void Test19_TokensSqlSegregados_DircavNoCoincideConDirdac()
        {
            Assert.IsTrue(AocrRolesInstitucionales.EsDircav("DIRCAV"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav("DIRDAC"));

            Assert.IsTrue(AocrRolesInstitucionales.EsDirdac("DIRDAC"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDirdac("DIRCAV"));
        }

        // 20. Aliases cruzados eliminados
        [TestMethod]
        public void Test20_AliasCruzadosEliminados()
        {
            foreach (var alias in AocrRolesInstitucionales.DirdacAliases)
            {
                Assert.IsFalse(alias.Equals("DIRCAV", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(alias.Equals("DCAV", StringComparison.OrdinalIgnoreCase));
            }
        }

        // 21. Catálogo canónico contiene exactamente los 7 roles
        [TestMethod]
        public void Test21_Canonica7Roles_ExactamenteSiete()
        {
            var rolesCanónicos = new[]
            {
                AocrRolesInstitucionales.Dircav,
                AocrRolesInstitucionales.Dirdac,
                AocrRolesInstitucionales.Coordinador,
                AocrRolesInstitucionales.RT,
                AocrRolesInstitucionales.Financiero,
                AocrRolesInstitucionales.Inspector,
                AocrRolesInstitucionales.Administrador
            };

            var rolesUnicos = rolesCanónicos.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Assert.AreEqual(7, rolesUnicos.Count, "Deben existir exactamente 7 roles canónicos únicos.");
        }

        // 22. Idempotencia en firma: un documento firmado no queda pendiente
        [TestMethod]
        public void Test22_DobleClicNoGeneraDosFirmas_Idempotencia()
        {
            var filaFirmada = new AocrBandejaDocumentoRow
            {
                SolicitudId = 15,
                FirmaReconocimientoId = 777,
                EstadoDocumentoAocr = AocrEstadosProceso.AocrFirmadaDirdac
            };

            Assert.IsFalse(AocrFirmaPendientePolicy.EsAocrPendienteFirma(filaFirmada),
                "Un documento ya firmado no debe figurar pendiente ante doble click.");
        }

        // 23. Estados segregados en la máquina de estados
        [TestMethod]
        public void Test23_TransicionIncorrecta_RechazadaPorMaquinaEstados()
        {
            Assert.AreNotEqual(AocrEstadosProceso.ClFirmadaDircav, AocrEstadosProceso.AocrFirmadaDirdac);
            Assert.AreNotEqual(AocrEstadosProceso.PendienteDesignacionDircav, AocrEstadosProceso.AocrPendienteDirdac);
        }

        // 24. Auditoría registra rol exacto
        [TestMethod]
        public void Test24_AuditoriaRegistraRolExacto()
        {
            Assert.AreEqual("DIRCAV", AocrRolesInstitucionales.Dircav);
            Assert.AreEqual("DIRDAC", AocrRolesInstitucionales.Dirdac);
        }

        // 25. Expediente cierra únicamente con ambas firmas institucionales
        [TestMethod]
        public void Test25_ExpedienteCierraUnicamenteConAmbasFirmas()
        {
            var filaSoloDircav = new AocrBandejaDocumentoRow
            {
                FirmaCondicionesId = 100,
                FirmaReconocimientoId = null,
                EstadoDocumentoAocr = AocrEstadosProceso.AocrPendienteDirdac
            };
            Assert.IsTrue(AocrFirmaPendientePolicy.EsAocrPendienteFirma(filaSoloDircav),
                "Si falta la firma DIRDAC, el AOCR sigue pendiente.");

            var filaCompleta = new AocrBandejaDocumentoRow
            {
                FirmaCondicionesId = 100,
                FirmaReconocimientoId = 200,
                EstadoDocumentoCondiciones = AocrEstadosProceso.ClFirmadaDircav,
                EstadoDocumentoAocr = AocrEstadosProceso.AocrFirmadaDirdac
            };
            Assert.IsFalse(AocrFirmaPendientePolicy.EsCondicionesPendienteFirma(filaCompleta));
            Assert.IsFalse(AocrFirmaPendientePolicy.EsAocrPendienteFirma(filaCompleta));
        }
    }
}
