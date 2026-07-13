using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class EnvioDocumentosDcavIntegrationTests
    {
        [TestMethod] public void FlujoIntegraControladorServicioDaoYEstado(){var total=Fuente("CapaPresentacion\\Controllers\\InspectorDocumentosFinalesController.cs")+Fuente("CapaNegocio\\Services\\EnvioDocumentosDcavService.cs")+Fuente("CapaDatos\\DAOs\\EnvioDocumentosDcavDAO.cs");foreach(var x in new[]{"FinalizarYEnviarDcav","FinalizarYEnviar(request)","MarcarDocumentosEnviados","CambiarEstadoCentral","PENDIENTE_REVISION_DOCUMENTOS_DCAV"})StringAssert.Contains(total,x);}
        [TestMethod] public void PaqueteNoPuedeConfirmarseParcialmente(){var s=Fuente("CapaNegocio\\Services\\EnvioDocumentosDcavService.cs");var a=s.IndexOf("MarcarDocumentosEnviados",StringComparison.Ordinal);var e=s.IndexOf("tx.Commit()",a,StringComparison.Ordinal);Assert.IsTrue(a>0&&e>a);}
        [TestMethod] public void TrazabilidadYNotificacionOcurrenAntesDelCommit(){var s=Fuente("CapaNegocio\\Services\\EnvioDocumentosDcavService.cs");var h=s.IndexOf("RegistrarHistorial",StringComparison.Ordinal);var n=s.IndexOf("CrearNotificacionesDcav",StringComparison.Ordinal);var c=s.IndexOf("tx.Commit()",h,StringComparison.Ordinal);Assert.IsTrue(h>0&&n>h&&c>n);}
        [TestMethod] public void CorreoSeEncolaDespuesDelCommit(){var s=Fuente("CapaNegocio\\Services\\EnvioDocumentosDcavService.cs");Assert.IsTrue(s.IndexOf("tx.Commit()",StringComparison.Ordinal)<s.IndexOf("EncolarCorreosPostCommit",StringComparison.Ordinal));}
        [TestMethod] public void ScriptsIncluyenDeployYRollback(){StringAssert.Contains(Fuente("scripts\\007_envio_documentos_dcav.sql"),"MIGRACION_007");StringAssert.Contains(Fuente("scripts\\007_envio_documentos_dcav_rollback.sql"),"DROP INDEX");}
        private static string Fuente(string relativa){var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;Assert.IsNotNull(d);return File.ReadAllText(Path.Combine(d.FullName,relativa));}
    }
}
