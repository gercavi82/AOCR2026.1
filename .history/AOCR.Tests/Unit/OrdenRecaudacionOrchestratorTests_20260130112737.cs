using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AOCR.Tests.Mocks;
using CapaNegocio.DTOs;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class OrdenRecaudacionOrchestratorTests
    {
        private MockOrdenRecaudacionRepository _ordenRepo;
        private MockPagoRepository _pagoRepo;
        private MockEmailService _emailService;
        private OrdenRecaudacionOrchestrator _orchestrator;

        [TestInitialize]
        public void Setup()
        {
            _ordenRepo = new MockOrdenRecaudacionRepository();
            _pagoRepo = new MockPagoRepository();
            _emailService = new MockEmailService();

            _orchestrator = new OrdenRecaudacionOrchestrator(
                _ordenRepo,
                _pagoRepo,
                null, // contribuyenteRepo
                null, // pdfService
                _emailService,
                null  // fileService
            );
        }

        [TestMethod]
        [Description("Test 1: Crear orden exitosamente")]
        public async Task CrearOrden_DatosValidos_RetornaExito()
        {
            // Arrange
            var request = new CrearOrdenRequest
            {
                ConceptoId = 1,
                ContribuyenteId = 1,
                Subtotal = 100m,
                Iva = 12m,
                Total = 112m,
                UsuarioCreacion = "test_user"
            };

            // Act
            var result = await _orchestrator.CrearOrdenAsync(request);

            // Assert
            Assert.IsTrue(result.Success, "Debería crear orden exitosamente");
            Assert.IsNotNull(result.Data);
            Assert.IsTrue(result.Data.OrdenId > 0);
            Assert.AreEqual("PENDIENTE", result.Data.Estado);
        }

        [TestMethod]
        [Description("Test 2: Crear orden sin contribuyente falla validación")]
        public async Task CrearOrden_SinContribuyente_RetornaError()
        {
            // Arrange
            var request = new CrearOrdenRequest
            {
                ConceptoId = 1,
                ContribuyenteId = 0, // Inválido
                Total = 100m,
                UsuarioCreacion = "test_user"
            };

            // Act
            var result = await _orchestrator.CrearOrdenAsync(request);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual("VALIDATION_ERROR", result.ErrorCode);
        }

        [TestMethod]
        [Description("Test 3: Registrar pago en orden pendiente")]
        public async Task RegistrarPago_OrdenPendiente_RetornaExito()
        {
            // Arrange - Crear orden primero
            var ordenRequest = new CrearOrdenRequest
            {
                ConceptoId = 1,
                ContribuyenteId = 1,
                Total = 100m,
                UsuarioCreacion = "test_user"
            };
            var ordenResult = await _orchestrator.CrearOrdenAsync(ordenRequest);

            var pagoRequest = new RegistrarPagoRequest
            {
                OrdenId = ordenResult.Data.OrdenId,
                NumeroComprobante = "COMP-001",
                MontoPagado = 100m,
                FechaPago = DateTime.Today,
                UsuarioRegistro = "test_user"
            };

            // Act
            var result = await _orchestrator.RegistrarPagoAsync(pagoRequest);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("PROCESADA", result.Data.EstadoOrden);
        }

        [TestMethod]
        [Description("Test 4: Registrar pago en orden inexistente falla")]
        public async Task RegistrarPago_OrdenInexistente_RetornaError()
        {
            // Arrange
            var request = new RegistrarPagoRequest
            {
                OrdenId = 9999,
                NumeroComprobante = "COMP-001",
                MontoPagado = 100m,
                FechaPago = DateTime.Today,
                UsuarioRegistro = "test_user"
            };

            // Act
            var result = await _orchestrator.RegistrarPagoAsync(request);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [TestMethod]
        [Description("Test 5: Flujo completo orden -> pago -> validación")]
        public async Task FlujoCompleto_OrdenPagoValidacion_EstadosCorrectos()
        {
            // Arrange & Act - Paso 1: Crear orden
            var ordenResult = await _orchestrator.CrearOrdenAsync(new CrearOrdenRequest
            {
                ConceptoId = 1,
                ContribuyenteId = 1,
                Total = 100m,
                UsuarioCreacion = "test_user"
            });
            Assert.IsTrue(ordenResult.Success);
            Assert.AreEqual("PENDIENTE", ordenResult.Data.Estado);

            // Paso 2: Registrar pago
            var pagoResult = await _orchestrator.RegistrarPagoAsync(new RegistrarPagoRequest
            {
                OrdenId = ordenResult.Data.OrdenId,
                NumeroComprobante = "COMP-001",
                MontoPagado = 100m,
                FechaPago = DateTime.Today,
                UsuarioRegistro = "test_user"
            });
            Assert.IsTrue(pagoResult.Success);
            Assert.AreEqual("PROCESADA", pagoResult.Data.EstadoOrden);

            // Paso 3: Validar pago
            var validarResult = await _orchestrator.ValidarPagoAsync(new ValidarPagoRequest
            {
                PagoId = pagoResult.Data.PagoId,
                Aprobado = true,
                Observaciones = "Pago verificado",
                UsuarioValidacion = "financiero_user"
            });
            Assert.IsTrue(validarResult.Success);
            Assert.AreEqual("FACTURADA", validarResult.Data.EstadoOrden);
        }
    }
}
