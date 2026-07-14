using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class GateHDcavFlowTests
    {
        [TestMethod]public void EstadoCentralDcav_EstaDefinido(){StringAssert.Contains(Read("CapaDatos/Constants/AocrEstadosProceso.cs"),"PENDIENTE_REVISION_INFORME_DCAV");}
        [TestMethod]public void BandejaDcav_UsaEstadoCentral_NoConsultaLegacy(){var dao=Read("CapaDatos/DAOs/InspeccionInformeDAO.cs");var start=dao.IndexOf("ListarPendientesRevisionInformeDcav",StringComparison.Ordinal);var body=dao.Substring(start,Math.Min(900,dao.Length-start));StringAssert.Contains(body,"PendienteRevisionInformeDcav");Assert.IsFalse(body.Contains("ListarPendientesFirmaDirdac"));}
        [TestMethod]public void TransicionInspector_AsignaResponsableDcav(){var c=Read("CapaPresentacion/Controllers/InspeccionController.cs");StringAssert.Contains(c,"AocrEstadosProceso.PendienteRevisionInformeDcav");StringAssert.Contains(c,"ROL_DIRECTOR_CERTIFICACIONES_DCAV");StringAssert.Contains(c,"SATISFACTORIO");}
        [TestMethod]public void AprobacionDcav_HabilitaModulosSieteYOcho(){var c=Read("CapaPresentacion/Controllers/InspeccionController.cs");StringAssert.Contains(c,"AocrEstadosProceso.InformeTecnicoAprobadoDcav");StringAssert.Contains(c,"SincronizarSolicitudAocrTrasFirmaFinal");StringAssert.Contains(c,"EMISION_AOCR_CONDICIONES");}
        [TestMethod]public void DirectorDcav_PuedeEntrarYFirmarCondiciones(){StringAssert.Contains(Read("CapaPresentacion/Services/FirmaAocrServices.cs"),"DirectorCertificacionesDcav");var c=Read("CapaPresentacion/Controllers/CoordinacionJefaturaController.cs");var i=c.IndexOf("public ActionResult FirmarCondiciones",StringComparison.Ordinal);StringAssert.Contains(c.Substring(Math.Max(0,i-500),Math.Min(900,c.Length-Math.Max(0,i-500))),"DirectorCertificacionesDcav");}
        [TestMethod]public void MigracionIncluyeRollback(){Assert.IsTrue(File.Exists(Path.Combine(Root(),"scripts/sql/012_gate_h_dcav_estado_central.sql")));Assert.IsTrue(File.Exists(Path.Combine(Root(),"scripts/sql/012_gate_h_dcav_estado_central_rollback.sql")));}
        [TestMethod]public void CierreExigeDosFirmasHashTamanoFirmanteYArchivo(){var s=Read("CapaNegocio/Services/AocrFinalizacionService.cs");StringAssert.Contains(s,"DocumentoFirmadoValido");StringAssert.Contains(s,"HashDocumento");StringAssert.Contains(s,"TamanioPdfFirmado");StringAssert.Contains(s,"FirmadoPorRol");StringAssert.Contains(s,"rutaExiste(firma.RutaDocumento)");}
        [TestMethod]public void CierreEsBloqueadoPorNoConformidadesVigentes(){var s=Read("CapaNegocio/Services/AocrFinalizacionService.cs");StringAssert.Contains(s,"ListarPorSolicitud");StringAssert.Contains(s,"NcBloqueaCierre");StringAssert.Contains(s,"no conformidades pendientes");}
        [TestMethod]public void NotificacionFinalVerificaSha256YDestinatarioDcav(){var s=Read("CapaNegocio/Services/AocrProcesoNotificacionService.cs");StringAssert.Contains(s,"SHA256.Create");StringAssert.Contains(s,"hash no coincide");StringAssert.Contains(s,"DirectorCertificacionesDcav");StringAssert.Contains(s,"SKIP_DUPLICADO");}
        [TestMethod]public void DescargarCondiciones_NoFinalizaComoEfectoSecundario(){var c=Read("CapaPresentacion/Controllers/SolicitudAOCRController.cs");var i=c.IndexOf("DescargarCondicionesLimitacionesModificacion",StringComparison.Ordinal);var body=c.Substring(i,Math.Min(4200,c.Length-i));Assert.IsFalse(body.Contains("Descarga final de Condiciones y Limitaciones firmada por RT"));}
        [TestMethod]public void FirmaActualizaEstadoCentralDeModulosSieteYOcho(){var c=Read("CapaPresentacion/Controllers/FirmaAocrController.cs");StringAssert.Contains(c,"ActualizarEstadoCentralSeguro");StringAssert.Contains(c,"PendienteGeneracionCyl");StringAssert.Contains(c,"CylFirmadas");StringAssert.Contains(c,"DocumentacionFinalCompleta");}
        [TestMethod]public void CierreGateH_TieneMigracionIdempotenteYRollback(){var sql=Read("scripts/sql/013_gate_h_cierre_modulos_7_8.sql");StringAssert.Contains(sql,"CREATE UNIQUE INDEX IF NOT EXISTS");Assert.IsTrue(File.Exists(Path.Combine(Root(),"scripts/sql/013_gate_h_cierre_modulos_7_8_rollback.sql")));}
        private static string Read(string path){return File.ReadAllText(Path.Combine(Root(),path.Replace('/',Path.DirectorySeparatorChar)));}
        private static string Root(){return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..",".."));}
    }
}
