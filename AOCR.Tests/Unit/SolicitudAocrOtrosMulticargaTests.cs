using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class SolicitudAocrOtrosMulticargaTests
    {
        [TestMethod]
        public void OtrosAdicionales_PermiteSeleccionMultipleYEnviaTodosLosArchivos()
        {
            var view = Read("CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml");

            StringAssert.Contains(view, "id=\"archivoOtro\" name=\"archivoOtro\"");
            StringAssert.Contains(view, "accept=\".pdf,.jpg,.jpeg,.png\" multiple");
            StringAssert.Contains(view, "id=\"dropzoneArchivoOtro\"");
            StringAssert.Contains(view, "id=\"listaArchivoOtro\"");
            StringAssert.Contains(view, "inicializarDropzoneCarga('archivoOtro', 'dropzoneArchivoOtro')");
            StringAssert.Contains(view, "['archivoCertificadoRuido', 'archivoCertificadoAeronavegabilidad', 'archivoOtro']");
            StringAssert.Contains(view, "formData.append(inputId, input.files[f])");
        }

        [TestMethod]
        public void Backend_ProcesaCadaArchivoRecibidoConLaMismaClave()
        {
            var controller = Read("CapaPresentacion/Controllers/SolicitudAOCRController.cs");

            StringAssert.Contains(controller, "for (var i = 0; i < archivos.Count; i++)");
            StringAssert.Contains(controller, "var inputKey = (archivos.GetKey(i) ?? string.Empty).Trim()");
            StringAssert.Contains(controller, "_documentoDAO.Crear(doc)");
        }

        [TestMethod]
        public void RevisionDocumental_NoAgrupaArchivosIndependientesDeCamposMulticarga()
        {
            var helper = Read("CapaPresentacion/Helpers/RevisionDocumentalDisplayHelper.cs");
            var listadoController = Read("CapaPresentacion/Controllers/DocumentoController.cs");
            var revisionController = Read("CapaPresentacion/Controllers/RevisionDocumentalController.cs");

            StringAssert.Contains(helper, "AllowsMultipleActiveDocuments");
            StringAssert.Contains(helper, "CERTIFICADO_AERONAVEGABILIDAD");
            StringAssert.Contains(helper, "CERTIFICADO_RUIDO_AERONAVES_EAE");
            StringAssert.Contains(helper, "OTROS_ADICIONALES");
            StringAssert.Contains(listadoController, "RevisionDocumentalDisplayHelper.AllowsMultipleActiveDocuments(tipoDocumento)");
            StringAssert.Contains(revisionController, "RevisionDocumentalDisplayHelper.AllowsMultipleActiveDocuments(tipoDocumento)");
            StringAssert.Contains(revisionController, "return \"__DOC_\" + documento.CodigoDocumento;");
        }

        private static string Read(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            var absolutePath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

            Assert.IsTrue(File.Exists(absolutePath), "No se encontro el archivo: " + absolutePath);
            return File.ReadAllText(absolutePath);
        }
    }
}
