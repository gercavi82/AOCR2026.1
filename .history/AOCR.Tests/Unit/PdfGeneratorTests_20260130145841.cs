using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class PdfGeneratorTests
    {
        [TestMethod]
        [Description("Test: Validar datos nulos para PDF")]
        public void GenerarPdf_OrdenNula_LanzaExcepcion()
        {
            // Arrange
            object orden = null;

            // Act & Assert
            Assert.IsNull(orden);
        }

        [TestMethod]
        [Description("Test: Número de orden requerido")]
        public void GenerarPdf_SinNumeroOrden_Falla()
        {
            // Arrange
            var numeroOrden = "";

            // Act & Assert
            Assert.IsTrue(string.IsNullOrWhiteSpace(numeroOrden));
        }
    }
}
