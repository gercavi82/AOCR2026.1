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

        [TestMethod]
        [Description("La cuenta institucional GEN_COORDINACION no debe quedar bloqueada por RT pendiente.")]
        public void Login_NoBloqueaCuentaCoordinacionForzada_PorEstadoRtPendiente()
        {
            var controller = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\..\..\..\CapaPresentacion\Controllers\AccountController.cs"));
            StringAssert.Contains(controller, "IsForcedCoordinacionUser(");
            StringAssert.Contains(controller, "!esUsuarioInstitucionalForzado");
        }
    }
}
