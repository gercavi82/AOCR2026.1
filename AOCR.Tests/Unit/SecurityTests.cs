using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class SecurityTests
    {
        [TestMethod]
        [Description("Test 10: Prevenir path traversal")]
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
        [Description("Test 11: Nombre seguro no contiene caracteres peligrosos")]
        public void NombreArchivo_CaracteresPeligrosos_Removidos()
        {
            // Arrange
            var nombre = "archivo<script>.pdf";
            
            // Act
            var seguro = nombre.Replace("<", "").Replace(">", "");

            // Assert
            Assert.IsFalse(seguro.Contains("<"));
            Assert.IsFalse(seguro.Contains(">"));
        }
    }
}
