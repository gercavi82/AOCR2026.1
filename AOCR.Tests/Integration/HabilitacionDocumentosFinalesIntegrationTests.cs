using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class HabilitacionDocumentosFinalesIntegrationTests
    {
        [TestMethod]
        public void AprobacionDcav_DelegaEnCoordinadorYPropagaHttpReal()
        {
            var servicio=Fuente("CapaNegocio\\Services\\AocrDcavRevisionService.cs");
            var controlador=Fuente("CapaPresentacion\\Controllers\\AocrDcavController.cs");
            StringAssert.Contains(servicio,"_habilitacionDocumentosService.Habilitar(");
            StringAssert.Contains(controlador,"Response.StatusCode = result.Codigo");
        }

        [TestMethod]
        public void Coordinador_IntegraDocumentosEstadoHistorialAuditoriaYNotificacionEnUnaTransaccion()
        {
            var s=Fuente("CapaNegocio\\Services\\HabilitacionDocumentosFinalesService.cs");
            var inicio=s.IndexOf("BeginTransaction",StringComparison.Ordinal);
            var commit=s.LastIndexOf("tx.Commit()",StringComparison.Ordinal);
            Assert.IsTrue(inicio>=0 && commit>inicio);
            foreach(var operacion in new[]{"_aocr.ObtenerOCrearBorrador","_condiciones.ObtenerOCrearBorrador","CambiarEstadoEnTransaccion","RegistrarAuditoria","CrearNotificacionInspector","RegistrarIdempotencia"})
            {
                var posicion=s.IndexOf(operacion,inicio,StringComparison.Ordinal);
                Assert.IsTrue(posicion>inicio && posicion<commit,operacion+" debe ejecutarse antes del commit.");
            }
        }

        [TestMethod]
        public void Inspector_IntegraMismaElegibilidadEnBandejaContadorYDosBloques()
        {
            var bandeja=Fuente("CapaNegocio\\Services\\InspectorBandejaService.cs");
            var firma=Fuente("CapaPresentacion\\Services\\FirmaAocrServices.cs");
            var vista=Fuente("CapaPresentacion\\Views\\FirmaAocr\\Index.cshtml");
            StringAssert.Contains(bandeja,"TieneParDocumentosInspector");
            StringAssert.Contains(bandeja,"DocumentosAocr = ObtenerDocumentosPendientesRevision(context).Count");
            StringAssert.Contains(firma,"\"RECONOCIMIENTO\", \"Documento AOCR\"");
            StringAssert.Contains(firma,"\"CONDICIONES_LIMITACIONES\", \"Documento Condiciones\"");
            StringAssert.Contains(vista,"v@doc.Version");
        }

        private static string Fuente(string relativa)
        {
            var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;
            Assert.IsNotNull(d,"No se encontro la raiz del repositorio.");
            return File.ReadAllText(Path.Combine(d.FullName,relativa));
        }
    }
}
