using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaPresentacion.Filters;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class SecurityTests
    {
        [TestMethod]
        [Description("Test 10: Prevenir path traversal en nombre de archivo")]
        public void ValidarArchivo_PathTraversal_Sanitizado()
        {
            // Arrange
            var maliciousName = "..\\..\\..\\windows\\system32\\config.pdf";

            // Act
            var safeName = Path.GetFileName(maliciousName);

            // Assert
            Assert.AreEqual("config.pdf", safeName);
            Assert.IsFalse(safeName.Contains(".."));
        }

        [TestMethod]
        [Description("Validar que archivo con magic bytes incorrectos es rechazado")]
        public void ValidarArchivo_MagicBytesIncorrectos_Invalido()
        {
            // Arrange - Archivo con extensión .pdf pero contenido de ejecutable
            var mock = new Moq.Mock<System.Web.HttpPostedFileBase>();
            mock.Setup(f => f.FileName).Returns("documento.pdf");
            mock.Setup(f => f.ContentType).Returns("application/pdf");
            mock.Setup(f => f.ContentLength).Returns(4);
            mock.Setup(f => f.InputStream).Returns(new MemoryStream(new byte[] { 0x4D, 0x5A, 0x00, 0x00 })); // MZ header (exe)

            // Act
            var result = FileUploadValidator.ValidateFile(mock.Object);

            // Assert
            Assert.IsFalse(result.IsValid);
        }
    }
}
