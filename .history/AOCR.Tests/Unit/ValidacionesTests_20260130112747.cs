using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaNegocio.DTOs;
using CapaNegocio.Services;
using CapaPresentacion.Filters;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class ValidacionesTests
    {
        [TestMethod]
        [Description("Test 6: Validar archivo con extensión permitida")]
        public void ValidarArchivo_ExtensionPdf_Valido()
        {
            // Arrange & Act
            var result = FileUploadValidator.ValidateFile(
                CreateMockFile("documento.pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 }));

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("application/pdf", result.DetectedType);
        }

        [TestMethod]
        [Description("Test 7: Validar archivo con extensión no permitida")]
        public void ValidarArchivo_ExtensionExe_Invalido()
        {
            // Arrange & Act
            var result = FileUploadValidator.ValidateFile(
                CreateMockFile("virus.exe", "application/octet-stream", new byte[] { 0x4D, 0x5A }));

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Error.Contains("no permitida"));
        }

        [TestMethod]
        [Description("Test 8: Validar monto negativo en orden")]
        public void ValidarCrearOrden_MontoNegativo_Invalido()
        {
            // Arrange
            var orchestrator = CreateTestOrchestrator();
            var request = new CrearOrdenRequest
            {
                ContribuyenteId = 1,
                ConceptoId = 1,
                Total = -100m, // Negativo
                UsuarioCreacion = "test"
            };

            // Act
            var result = orchestrator.ValidarCrearOrden(request);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Message.Contains("mayor a cero"));
        }

        [TestMethod]
        [Description("Test 9: Validar fecha de pago futura")]
        public void ValidarRegistrarPago_FechaFutura_Invalido()
        {
            // Arrange
            var orchestrator = CreateTestOrchestrator();
            var request = new RegistrarPagoRequest
            {
                OrdenId = 1,
                NumeroComprobante = "COMP-001",
                MontoPagado = 100m,
                FechaPago = DateTime.Now.AddDays(30), // Futura
                UsuarioRegistro = "test"
            };

            // Act
            var result = orchestrator.ValidarRegistrarPago(request);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Message.Contains("futura"));
        }

        private OrdenRecaudacionOrchestrator CreateTestOrchestrator()
        {
            return new OrdenRecaudacionOrchestrator(
                new AOCR.Tests.Mocks.MockOrdenRecaudacionRepository(),
                new AOCR.Tests.Mocks.MockPagoRepository(),
                null, null, null, null);
        }

        private System.Web.HttpPostedFileBase CreateMockFile(string name, string contentType, byte[] content)
        {
            var mock = new Moq.Mock<System.Web.HttpPostedFileBase>();
            mock.Setup(f => f.FileName).Returns(name);
            mock.Setup(f => f.ContentType).Returns(contentType);
            mock.Setup(f => f.ContentLength).Returns(content.Length);
            mock.Setup(f => f.InputStream).Returns(new System.IO.MemoryStream(content));
            return mock.Object;
        }
    }
}
