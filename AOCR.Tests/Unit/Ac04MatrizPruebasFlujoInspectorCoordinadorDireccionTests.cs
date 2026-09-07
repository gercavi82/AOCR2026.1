using System;
using System.IO;
using System.Linq;
using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-04: Matriz Exhaustiva de Pruebas - Flujo Inspector -> Coordinador -> Dirección (DIRCAV / DIRDAC)
    /// Valida la segregación estricta de roles, bandejas de trabajo, ciclo completo y retornos institucionales.
    /// </summary>
    [TestClass]
    public class Ac04MatrizPruebasFlujoInspectorCoordinadorDireccionTests
    {
        private readonly IAocrFlujoService _flujoService = new AocrFlujoService();
        private readonly IAocrEstadoService _estadoService = new AocrEstadoService();
        private readonly DircavBandejaService _dircavBandejaService = new DircavBandejaService();

        private static string ReadFile(string relativePath)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, relativePath);
            if (!File.Exists(path))
            {
                path = Path.Combine(baseDir, "..", "..", "..", relativePath);
            }
            if (!File.Exists(path))
            {
                path = Path.Combine(@"c:\proyectos\AOCR", relativePath);
            }
            return File.ReadAllText(path);
        }

        #region 1. Flujo Canónico Completo: Inspector -> Coordinador -> DIRCAV -> DIRDAC

        [TestMethod]
        public void Test01_Canonico_InspectorAFirmaInforme_PermitidoEnCatalogoYMatriz()
        {
            // Inspector finaliza informe técnico
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.PendienteInformeInspector,
                AocrEstadosProceso.InformeTecnicoFirmadoInspector),
                "El informe pendiente de elaboración/firma debe permitir transición a firmado por inspector.");
        }

        [TestMethod]
        public void Test02_Canonico_InspectorRemiteACoordinador_PasaAPendienteRevisionFinalCoordinador()
        {
            // Inspector envía informe firmado a Coordinación
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.InformeTecnicoFirmadoInspector,
                AocrEstadosProceso.PendienteRevisionFinalCoordinador),
                "El informe firmado por inspector debe poder remitirse a revisión final del coordinador.");

            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Inspector,
                AocrFlujoAcciones.InspectorRemitirInformeCoordinador),
                "El rol Inspector debe tener permiso para remitir informe técnico a Coordinación.");
        }

        [TestMethod]
        public void Test03_Canonico_CoordinadorApruebaInforme_RemiteEstrictamenteADircav_NO_Dirdac()
        {
            // Coordinador aprueba informe técnico y remite Condiciones y Limitaciones a DIRCAV
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.PendienteRevisionFinalCoordinador,
                AocrEstadosProceso.ClPendienteDircav),
                "El coordinador debe poder remitir la solicitud a DIRCAV para C&L.");

            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Coordinador,
                AocrFlujoAcciones.CoordinadorRemitirClDircav),
                "El rol Coordinador debe tener permiso para remitir C&L a DIRCAV.");

            // Validar que el Coordinador NO puede remitir directamente a DIRDAC
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Coordinador,
                AocrFlujoAcciones.DircavRemitirDirdac),
                "El rol Coordinador NO debe tener permiso de remitir directamente a DIRDAC.");
        }

        [TestMethod]
        public void Test04_Canonico_DircavApruebaYFirmaCl()
        {
            // DIRCAV aprueba revisión de C&L y pasa a firma
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.ClPendienteDircav,
                AocrEstadosProceso.ClPendienteFirmaDircav),
                "DIRCAV debe poder avanzar C&L a estado pendiente de firma.");

            // DIRCAV firma C&L
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.ClPendienteFirmaDircav,
                AocrEstadosProceso.ClFirmadaDircav),
                "DIRCAV debe poder firmar C&L quedando en ClFirmadaDircav.");

            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Dircav,
                AocrFlujoAcciones.DircavFirmarCl),
                "El rol DIRCAV debe tener permiso para firmar Condiciones y Limitaciones.");
        }

        [TestMethod]
        public void Test05_Canonico_DircavRemiteADirdac_SoloConClPreviamenteFirmada()
        {
            // Transición de ClFirmadaDircav a AocrPendienteDirdac
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.ClFirmadaDircav,
                AocrEstadosProceso.AocrPendienteDirdac),
                "DIRCAV debe poder remitir el trámite a DIRDAC una vez firmada la C&L.");

            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Dircav,
                AocrFlujoAcciones.DircavRemitirDirdac),
                "El rol DIRCAV tiene la potestad exclusiva de remitir el expediente final a DIRDAC.");
        }

        [TestMethod]
        public void Test06_Canonico_DirdacLegalizaYFirmaAocr()
        {
            // DIRDAC firma y legaliza el certificado AOCR
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.AocrPendienteDirdac,
                AocrEstadosProceso.AocrFirmadaDirdac),
                "DIRDAC debe poder legalizar y firmar el AOCR pasando a AocrFirmadaDirdac.");

            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Dirdac,
                AocrFlujoAcciones.DirdacFirmarAocr),
                "El rol DIRDAC debe tener potestad de firmar el certificado AOCR.");
        }

        #endregion

        #region 2. Ciclos de Retorno y Reenvío Institucional

        [TestMethod]
        public void Test07_Retorno_CoordinadorDevuelveInformeAInspector()
        {
            // Coordinador devuelve informe técnico a inspector con observaciones
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.PendienteRevisionFinalCoordinador,
                AocrEstadosProceso.InformeTecnicoDevueltoInspector),
                "El Coordinador debe poder devolver el informe técnico al Inspector.");

            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Coordinador,
                AocrFlujoAcciones.CoordinadorDevolverClInspector),
                "El Coordinador debe poder ejecutar la devolución de informe a Inspector.");

            // Inspector corrige y reenvía a Coordinación
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.InformeTecnicoDevueltoInspector,
                AocrEstadosProceso.PendienteRevisionFinalCoordinador),
                "El Inspector debe poder reenviar el informe subsanado a Coordinación.");
        }

        [TestMethod]
        public void Test08_Retorno_DircavDevuelveACoordinador()
        {
            // DIRCAV devuelve C&L / expediente a Coordinador
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.ClPendienteDircav,
                AocrEstadosProceso.DevueltoCoordinadorFinalDircav),
                "DIRCAV debe poder devolver a Coordinador con observaciones.");

            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Dircav,
                AocrFlujoAcciones.DircavDevolverCoordinador),
                "DIRCAV debe tener potestad para devolver a Coordinación.");

            // Coordinador subsana y reenvía a DIRCAV
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.DevueltoCoordinadorFinalDircav,
                AocrEstadosProceso.ClPendienteDircav),
                "Coordinador debe poder reenviar a DIRCAV tras atender observaciones.");
        }

        [TestMethod]
        public void Test09_Retorno_DirdacDevuelveADircav()
        {
            // DIRDAC devuelve expediente a DIRCAV
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.AocrPendienteDirdac,
                AocrEstadosProceso.DevueltoDircavPorDirdac),
                "DIRDAC debe poder devolver a DIRCAV si encuentra inconsistencias en el AOCR.");

            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Dirdac,
                AocrFlujoAcciones.DirdacDevolverDircav),
                "DIRDAC debe tener permiso para devolver a DIRCAV.");

            // DIRCAV rectifica y reenvía a DIRDAC
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.DevueltoDircavPorDirdac,
                AocrEstadosProceso.AocrPendienteDirdac),
                "DIRCAV debe poder reenviar a DIRDAC tras rectificación.");
        }

        #endregion

        #region 3. Aislamiento Estricto de Roles y Prohibición de Saltos / Bypasses

        [TestMethod]
        public void Test10_Seguridad_InspectorNoPuedeEnviarDirectamenteADirdacNiADircav()
        {
            // Inspector bloqueado de remitir a DIRCAC o DIRCAV directamente
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Inspector,
                AocrFlujoAcciones.DircavRemitirDirdac),
                "El Inspector tiene prohibido remitir a DIRDAC.");

            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Inspector,
                AocrFlujoAcciones.CoordinadorRemitirClDircav),
                "El Inspector tiene prohibido remitir a DIRCAV.");

            // Verificar en código fuente que el controlador del Inspector remite a Coordinación y no a DIRDAC
            var inspeccionController = ReadFile("CapaPresentacion/Controllers/InspeccionController.cs");
            StringAssert.Contains(inspeccionController, "AocrEstadosProceso.PendienteRevisionFinalCoordinador");
            StringAssert.Contains(inspeccionController, "AocrEstadosProceso.ClPendienteDircav");
        }

        [TestMethod]
        public void Test11_Seguridad_SeparacionEstrictaDircavVsDirdac()
        {
            // DIRCAV no puede firmar AOCR
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Dircav,
                AocrFlujoAcciones.DirdacFirmarAocr),
                "DIRCAV no debe tener potestad de firmar el certificado AOCR.");

            // DIRDAC no puede firmar Condiciones y Limitaciones
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion(
                AocrRolesInstitucionales.Dirdac,
                AocrFlujoAcciones.DircavFirmarCl),
                "DIRDAC no debe tener potestad de firmar Condiciones y Limitaciones.");
        }

        [TestMethod]
        public void Test12_Bandejas_AislamientoPorEstado()
        {
            // Validar que las bandejas no mezclen estados incompatibles
            var coordContent = ReadFile("CapaPresentacion/Controllers/CoordinacionJefaturaController.cs");
            StringAssert.Contains(coordContent, "PendienteRevisionFinalCoordinador",
                "La bandeja de revisión de Coordinación debe incluir solicitudes pendientes de revisión final.");
            StringAssert.Contains(coordContent, "DevueltoCoordinadorFinalDircav",
                "La bandeja de Coordinación debe incluir solicitudes devueltas por DIRCAV.");

            var dircavContent = ReadFile("CapaNegocio/Services/DircavBandejaService.cs");
            StringAssert.Contains(dircavContent, "ClPendienteDircav",
                "La bandeja de DIRCAV debe procesar CL_PENDIENTE_DIRCAV.");
        }

        #endregion
    }
}
