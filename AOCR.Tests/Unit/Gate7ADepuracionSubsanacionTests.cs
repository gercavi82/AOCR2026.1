using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class Gate7ADepuracionSubsanacionTests
    {
        [TestMethod]
        public void EndpointLegacyPost_BloqueaEscrituraAntesDeProcesarArchivo()
        {
            var method = Slice(Read("CapaPresentacion/Controllers/RTController.cs"), "ActionResult SubsanarNcPost", "ActionResult DescargarSubsanacionNc");
            Assert.IsTrue(method.IndexOf("LEGACY_WRITE_BLOCKED", StringComparison.Ordinal) < method.IndexOf("SaveAs", StringComparison.Ordinal));
            Assert.IsTrue(method.IndexOf("RedirectToAction(\"Subsanar\", \"SolicitudAOCR\"", StringComparison.Ordinal) < method.IndexOf("SaveAs", StringComparison.Ordinal));
        }

        [TestMethod]
        public void EndpointLegacyGet_RedirigeAlFlujoIndividual()
        {
            var method = Slice(Read("CapaPresentacion/Controllers/RTController.cs"), "ActionResult SubsanarNc(", "ActionResult SubsanarNcPost");
            StringAssert.Contains(method, "LEGACY_REDIRECT");
            StringAssert.Contains(method, "RedirectToAction(\"Subsanar\", \"SolicitudAOCR\"");
        }

        [TestMethod]
        public void SubsanacionDevuelta_HabilitaVersionNMasUno()
        {
            var controller = Read("CapaPresentacion/Controllers/SolicitudAOCRController.cs");
            var dao = Read("CapaDatos/DAOs/DocumentoDAO.cs");
            StringAssert.Contains(controller, "SUBSANACION_DEVUELTA");
            StringAssert.Contains(dao, "versionAnterior + 1");
            StringAssert.Contains(dao, "'SUBSANACION_DEVUELTA'");
        }

        [TestMethod]
        public void DocumentoAceptado_NoPuedeSustituirse()
        {
            StringAssert.Contains(Read("CapaDatos/DAOs/DocumentoDAO.cs"), "ACEPTADO_SUBSANACION");
            StringAssert.Contains(Read("CapaDatos/DAOs/DocumentoDAO.cs"), "Un documento aceptado no puede ser reemplazado");
        }

        [TestMethod]
        public void DocumentoObservado_SiEsSubsanable()
        {
            var states = Read("CapaDatos/Constants/EstadoDocumentoInstitucional.cs");
            StringAssert.Contains(states, "Observado");
            StringAssert.Contains(states, "RechazadoSubsanacion");
        }

        [TestMethod]
        public void VistaDetalle_NoMuestraAccionGeneralYAccionIndividualSimultaneamente()
        {
            var view = Read("CapaPresentacion/Views/SolicitudAOCR/Detalle.cshtml");
            Assert.IsFalse(view.Contains("Url.Action(\"SubsanarNc\", \"RT\""));
            StringAssert.Contains(view, "Url.Action(\"Subsanar\", \"SolicitudAOCR\"");
        }

        [TestMethod]
        public void FormularioIndividual_ExigeTodosLosDocumentosObservados()
        {
            var controller = Read("CapaPresentacion/Controllers/SolicitudAOCRController.cs");
            StringAssert.Contains(controller, "documentosFaltantesSubsanacion");
            StringAssert.Contains(controller, "Debe subsanar todos los documentos observados/devueltos");
        }

        [TestMethod]
        public void CierreDocumental_NoCambiaResultadoTecnico()
        {
            var dao = Slice(Read("CapaDatos/DAOs/NoConformidadDAO.cs"), "AceptarSubsanacionDocumentalCompleta", "VincularNuevaEvaluacion");
            StringAssert.Contains(dao, "SUBSANACION_ACEPTADA");
            StringAssert.Contains(dao, "No se modifica resultado ni resultado_evaluacion");
            Assert.IsFalse(dao.Contains("SET resultado="));
            Assert.IsFalse(dao.Contains("resultado_evaluacion="));
        }

        [TestMethod]
        public void PdfGeneralHistorico_ConservaDescargaSegura()
        {
            var rt = Slice(Read("CapaPresentacion/Controllers/RTController.cs"), "ActionResult DescargarSubsanacionNc", "\n    }");
            StringAssert.Contains(rt, "RutaPdfSubsanacionRt");
            StringAssert.Contains(rt, "DocumentoSeguroService");
            StringAssert.Contains(rt, "FileStorageHelper");
        }

        [TestMethod]
        public void RtOtraCompania_Recibe403AntesDeRedireccionLegacy()
        {
            var method = Slice(Read("CapaPresentacion/Controllers/RTController.cs"), "ActionResult SubsanarNc(", "ActionResult SubsanarNcPost");
            Assert.IsTrue(method.IndexOf("EsPropietarioSolicitud", StringComparison.Ordinal) < method.IndexOf("LEGACY_REDIRECT", StringComparison.Ordinal));
            StringAssert.Contains(method, "HttpStatusCodeResult(403");
        }

        private static string Slice(string source, string start, string end)
        {
            var i = source.IndexOf(start, StringComparison.Ordinal); Assert.IsTrue(i >= 0, start);
            var j = source.IndexOf(end, i + start.Length, StringComparison.Ordinal); Assert.IsTrue(j > i, end);
            return source.Substring(i, j - i);
        }
        private static string Read(string path) { return File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar))); }
        private static string Root() { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")); }
    }
}
