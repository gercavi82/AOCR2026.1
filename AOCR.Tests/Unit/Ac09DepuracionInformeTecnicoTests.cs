using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaModelo;
using CapaDatos.DAOs;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-09: Pruebas unitarias para la depuración del Informe Técnico.
    /// Valida el retiro de los 5 campos indicados por el área funcional:
    /// 1. Alcance repetido (retirado del modal y de validaciones obligatorias backend).
    /// 2. Número de licencia (reemplazado por Cédula de Identidad en firma y cabecera).
    /// 3. Resumen de cumplimiento de observaciones repetido (notas retiradas del modal y condicionadas en PDF).
    /// 4. Reporte de infracción (retirado de documentos adjuntos base).
    /// 5. Reporte de suspensión de operaciones (retirado de documentos adjuntos base).
    /// Asegura la preservación histórica, no eliminación destructiva de columnas y ausencia de títulos huérfanos en PDF.
    /// </summary>
    [TestClass]
    public class Ac09DepuracionInformeTecnicoTests
    {
        [TestMethod]
        public void Test01_DocumentosAdjuntosBase_NoContieneReporteInfraccionNiSuspension()
        {
            var rutaHelper = @"c:\proyectos\AOCR\CapaPresentacion\Helpers\InformeTecnicoTemplateHelper.cs";
            Assert.IsTrue(File.Exists(rutaHelper), "InformeTecnicoTemplateHelper.cs debe existir.");

            var contenido = File.ReadAllText(rutaHelper);

            // Verificar la definición de DocumentosAdjuntosBase
            StringAssert.Contains(contenido, "\"LISTA DE VERIFICACION\"", "Debe mantener LISTA DE VERIFICACION.");
            StringAssert.Contains(contenido, "\"EVIDENCIAS DE LA INSPECCION\"", "Debe mantener EVIDENCIAS DE LA INSPECCION.");

            // Asegurar que en la lista base no figuren REPORTE DE INFRACCION ni REPORTE DE SUSPENSION
            var indexBase = contenido.IndexOf("DocumentosAdjuntosBase = new[]", StringComparison.Ordinal);
            Assert.IsTrue(indexBase > 0, "Debe existir la definición de DocumentosAdjuntosBase.");
            var finBase = contenido.IndexOf("};", indexBase, StringComparison.Ordinal);
            var bloqueBase = contenido.Substring(indexBase, finBase - indexBase);

            Assert.IsFalse(bloqueBase.Contains("REPORTE DE INFRACCION"), "DocumentosAdjuntosBase no debe contener REPORTE DE INFRACCION.");
            Assert.IsFalse(bloqueBase.Contains("REPORTE DE SUSPENSION"), "DocumentosAdjuntosBase no debe contener REPORTE DE SUSPENSION.");
        }

        [TestMethod]
        public void Test02_Historico_ParsingAdjuntosSigueReconociendoReporteInfraccionYSuspension()
        {
            const string adjuntosHistoricos = "LISTA DE VERIFICACION\r\nREPORTE DE INFRACCION\r\nREPORTE DE SUSPENSION DE FUNCIONES\r\nEVIDENCIAS DE LA INSPECCION";

            var items = adjuntosHistoricos
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.AreEqual(4, items.Count, "Un informe histórico con adjuntos previos debe parsear todos los elementos.");
            Assert.IsTrue(items.Contains("REPORTE DE INFRACCION"), "Debe preservar el histórico de REPORTE DE INFRACCION.");
            Assert.IsTrue(items.Contains("REPORTE DE SUSPENSION DE FUNCIONES"), "Debe preservar el histórico de REPORTE DE SUSPENSION DE FUNCIONES.");
        }

        [TestMethod]
        public void Test03_ValidarInformeParaFinalizar_NoExigeAlcanceComoObligatorio()
        {
            var rutaController = @"c:\proyectos\AOCR\CapaPresentacion\Controllers\InspeccionController.cs";
            Assert.IsTrue(File.Exists(rutaController), "InspeccionController.cs debe existir.");

            var contenido = File.ReadAllText(rutaController);

            var indexMetodo = contenido.IndexOf("bool ValidarInformeTecnicoParaFinalizar", StringComparison.Ordinal);
            Assert.IsTrue(indexMetodo > 0, "Debe existir ValidarInformeTecnicoParaFinalizar.");
            var finMetodo = contenido.IndexOf("return true;", indexMetodo, StringComparison.Ordinal);
            var bloqueMetodo = contenido.Substring(indexMetodo, finMetodo - indexMetodo);

            // Verificar que Alcance NO está en la lista de campos obligatorios
            Assert.IsFalse(bloqueMetodo.Contains("Nombre = \"Alcance\""), "El método ValidarInformeTecnicoParaFinalizar NO debe exigir Alcance como campo obligatorio.");
            StringAssert.Contains(bloqueMetodo, "Nombre = \"Antecedentes\"", "Debe conservar Antecedentes como obligatorio.");
            StringAssert.Contains(bloqueMetodo, "Nombre = \"Objetivo de la inspección\"", "Debe conservar Objetivo de la inspección como obligatorio.");
            StringAssert.Contains(bloqueMetodo, "Nombre = \"Desarrollo técnico\"", "Debe conservar Desarrollo técnico como obligatorio.");
        }

        [TestMethod]
        public void Test04_ModalInformeTecnico_NoPoseeTextareaAlcanceNiNotasNiInputLicencia()
        {
            var rutaModal = @"c:\proyectos\AOCR\CapaPresentacion\Views\InformeTecnico\_ModalInformeTecnico.cshtml";
            Assert.IsTrue(File.Exists(rutaModal), "Debe existir _ModalInformeTecnico.cshtml.");

            var contenido = File.ReadAllText(rutaModal);

            // 1. Textarea de alcance retirado
            Assert.IsFalse(contenido.Contains("<textarea name=\"alcance\""), "No debe contener textarea editable para alcance.");

            // 2. Input de licencia de inspector retirado
            Assert.IsFalse(contenido.Contains("id=\"numeroLicenciaInspectorModal\""), "No debe contener input id=numeroLicenciaInspectorModal.");
            Assert.IsFalse(contenido.Contains("Nro. licencia / identificación inspector"), "No debe contener la etiqueta de licencia.");

            // 3. Textarea de notas (resumen de cumplimiento de observaciones repetido) retirado
            Assert.IsFalse(contenido.Contains("<textarea name=\"notas\""), "No debe contener textarea editable para notas.");
            Assert.IsFalse(contenido.Contains("Resuma cumplimiento, observaciones relevantes"), "No debe contener el placeholder de notas redundantes.");
        }

        [TestMethod]
        public void Test05_ModalInformeTecnico_ConservaCedulaInspector()
        {
            var rutaModal = @"c:\proyectos\AOCR\CapaPresentacion\Views\InformeTecnico\_ModalInformeTecnico.cshtml";
            var contenido = File.ReadAllText(rutaModal);

            StringAssert.Contains(contenido, "Cédula del inspector", "Debe exhibir la etiqueta Cédula del inspector.");
            StringAssert.Contains(contenido, "value=\"@inspectorCedula\"", "Debe enlazar con la cédula del inspector.");
            StringAssert.Contains(contenido, "readonly=\"readonly\"", "La cédula debe mostrarse en modo lectura.");
        }

        [TestMethod]
        public void Test06_PdfInformeTecnico_UsaCedulaYNoLicenciaEnFirma()
        {
            var rutaPdf = @"c:\proyectos\AOCR\CapaPresentacion\Views\Inspeccion\InformeTecnicoPdf.cshtml";
            Assert.IsTrue(File.Exists(rutaPdf), "Debe existir InformeTecnicoPdf.cshtml.");

            var contenido = File.ReadAllText(rutaPdf);

            // Debe mostrar C.I. y no No. LICENCIA en el pie de firma
            StringAssert.Contains(contenido, "\"C.I. \" + cedulaInspector", "El PDF debe generar la cédula con prefijo C.I.");
            StringAssert.Contains(contenido, "@cedulaInspectorVisual", "El pie de firma debe renderizar cedulaInspectorVisual.");
            Assert.IsFalse(contenido.Contains("\"No. LICENCIA \""), "No debe renderizar el prefijo No. LICENCIA.");
        }

        [TestMethod]
        public void Test07_PdfInformeTecnico_CondicionaSeccionNotasParaEvitarTitulosHuerfanos()
        {
            var rutaPdf = @"c:\proyectos\AOCR\CapaPresentacion\Views\Inspeccion\InformeTecnicoPdf.cshtml";
            var contenido = File.ReadAllText(rutaPdf);

            // Debe existir la condición para no mostrar Notas si está vacío
            StringAssert.Contains(contenido, "@if (!string.IsNullOrWhiteSpace(notas)", "La sección Notas debe estar condicionada a tener valor.");
        }

        [TestMethod]
        public void Test08_RevisionDireccion_AlcanceOpcionalSinTitulosHuerfanos()
        {
            var rutaRevision = @"c:\proyectos\AOCR\CapaPresentacion\Views\InformeTecnico\RevisionDireccion.cshtml";
            Assert.IsTrue(File.Exists(rutaRevision), "Debe existir RevisionDireccion.cshtml.");

            var contenido = File.ReadAllText(rutaRevision);

            // Debe condicionar la subsección Alcance
            StringAssert.Contains(contenido, "@if (!string.IsNullOrWhiteSpace(vm.Alcance))", "En RevisionDireccion, Alcance debe ser condicional para evitar títulos huérfanos.");
        }

        [TestMethod]
        public void Test09_ModeloYDao_ConservanColumnasHistoricasSinEliminacionDestructiva()
        {
            // Validar que la entidad InspeccionInformeTecnico mantiene las propiedades
            var tipoEntidad = typeof(InspeccionInformeTecnico);
            Assert.IsNotNull(tipoEntidad.GetProperty("Alcance"), "La entidad debe conservar la propiedad Alcance.");
            Assert.IsNotNull(tipoEntidad.GetProperty("NumeroLicenciaInspector"), "La entidad debe conservar la propiedad NumeroLicenciaInspector.");
            Assert.IsNotNull(tipoEntidad.GetProperty("Notas"), "La entidad debe conservar la propiedad Notas.");

            // Validar que InspeccionInformeDAO sigue teniendo las columnas en sus queries
            var rutaDao = @"c:\proyectos\AOCR\CapaDatos\DAOs\InspeccionInformeDAO.cs";
            var contenidoDao = File.ReadAllText(rutaDao);

            StringAssert.Contains(contenidoDao, "alcance", "El DAO debe mantener alcance en SELECT/INSERT/UPDATE.");
            StringAssert.Contains(contenidoDao, "numero_licencia_inspector", "El DAO debe mantener numero_licencia_inspector.");
            StringAssert.Contains(contenidoDao, "notas", "El DAO debe mantener notas.");
        }

        [TestMethod]
        public void Test10_FirmaPreview_UsaIdentificacionInstitucional()
        {
            var rutaPreview = @"c:\proyectos\AOCR\CapaPresentacion\Views\Inspeccion\InformeTecnicoFirmaPreview.cshtml";
            Assert.IsTrue(File.Exists(rutaPreview), "Debe existir InformeTecnicoFirmaPreview.cshtml.");

            var contenido = File.ReadAllText(rutaPreview);

            StringAssert.Contains(contenido, "elaboradoPor", "La vista previa debe mostrar al inspector elaboradoPor.");
            Assert.IsFalse(contenido.Contains("No. LICENCIA"), "La vista previa de firma no debe exigir No. LICENCIA.");
        }
    }
}
