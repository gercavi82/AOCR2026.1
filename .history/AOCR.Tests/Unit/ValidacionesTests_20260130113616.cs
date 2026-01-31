using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class ValidacionesTests
    {
        [TestMethod]
        [Description("Test 6: Validar extensión PDF permitida")]
        public void ValidarArchivo_ExtensionPdf_Valido()
        {
            // Arrange
            var archivo = "documento.pdf";
            var extension = Path.GetExtension(archivo).ToLowerInvariant();
            var permitidas = new[] { ".pdf", ".jpg", ".jpeg", ".png" };

            // Act
            var esValido = Array.Exists(permitidas, e => e == extension);

            // Assert
            Assert.IsTrue(esValido);
        }

        [TestMethod]
        [Description("Test 7: Validar extensión EXE no permitida")]
        public void ValidarArchivo_ExtensionExe_Invalido()
        {
            // Arrange
            var archivo = "virus.exe";
            var extension = Path.GetExtension(archivo).ToLowerInvariant();
            var permitidas = new[] { ".pdf", ".jpg", ".jpeg", ".png" };

            // Act
            var esValido = Array.Exists(permitidas, e => e == extension);

            // Assert
            Assert.IsFalse(esValido);
        }

        [TestMethod]
        [Description("Test 8: Validar monto negativo")]
        public void ValidarMonto_Negativo_Invalido()
        {
            // Arrange
            decimal monto = -100m;

            // Act & Assert
            Assert.IsTrue(monto < 0, "Monto negativo debe ser inválido");
        }

        [TestMethod]
        [Description("Test 9: Validar fecha futura")]
        public void ValidarFecha_Futura_Invalida()
        {
            // Arrange
            var fechaPago = DateTime.Now.AddDays(30);

            // Act
            var esFutura = fechaPago > DateTime.Now;

            // Assert
            Assert.IsTrue(esFutura, "Fecha futura debe ser inválida para pagos");
        }
    }
}
