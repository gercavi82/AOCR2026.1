using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class RevisionDocumentosInspectorIntegrationTests
    {
        [TestMethod]
        public void RutaSidebar_Bandeja_Detalle_Formularios_ConservanSeccionExclusiva()
        {
            var sidebar=Fuente("CapaPresentacion\\Helpers\\SidebarMenuBuilder.cs");var controller=Fuente("CapaPresentacion\\Controllers\\InspectorDocumentosFinalesController.cs");var view=Fuente("CapaPresentacion\\Views\\InspectorDocumentosFinales\\Detalle.cshtml");
            StringAssert.Contains(sidebar,"\"InspectorDocumentosFinales\", \"Revision\"");StringAssert.Contains(controller,"View(\"~/Views/InspectorDocumentosFinales/Detalle.cshtml\"");StringAssert.Contains(view,"BeginForm(\"GuardarAocr\",\"InspectorDocumentosFinales\"");StringAssert.Contains(view,"BeginForm(\"GuardarCondiciones\",\"InspectorDocumentosFinales\"");
        }

        [TestMethod]
        public void GuardadoYGeneracion_IntegranAutorizacionVersionAuditoriaYPdf()
        {
            var s=Fuente("CapaPresentacion\\Services\\RevisionDocumentosInspectorService.cs");foreach(var token in new[]{"ContextoAutorizado","ValidarDocumento","ActualizarEdicionOptimista","RegistrarAuditoria","_documentoPdf.Generar","FirmaAocrPdfService"})StringAssert.Contains(s,token);
        }

        private static string Fuente(string relativa){var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;Assert.IsNotNull(d);return File.ReadAllText(Path.Combine(d.FullName,relativa));}
    }
}
