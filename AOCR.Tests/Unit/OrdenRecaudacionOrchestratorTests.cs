using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AOCR.Tests.Mocks;
using CapaDatos.Entidades;
using CapaDatos.Services;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class OrdenRecaudacionOrchestratorTests
    {
        private MockOrdenRecaudacionRepository _ordenRepo;
        private MockPagoRepository _pagoRepo;

        [TestInitialize]
        public void Setup()
        {
            _ordenRepo = new MockOrdenRecaudacionRepository();
            _pagoRepo = new MockPagoRepository();
        }

        [TestMethod]
        [Description("Test 1: Crear orden exitosamente")]
        public async Task CrearOrden_DatosValidos_RetornaExito()
        {
            // Arrange
            var orden = new OrdenRecaudacion
            {
                ConceptoId = 1,
                ContribuyenteId = 1,
                Subtotal = 100m,
                Iva = 12m,
                Total = 112m,
                UsuarioCreacion = "test_user",
                Estado = "PENDIENTE"
            };

            // Act
            var result = await _ordenRepo.CrearAsync(orden);

            // Assert
            Assert.IsTrue(result > 0, "Debería crear orden exitosamente");
        }

        [TestMethod]
        [Description("Test 2: Orden sin contribuyente tiene ContribuyenteId = 0")]
        public void CrearOrden_SinContribuyente_EsCero()
        {
            // Arrange
            var orden = new OrdenRecaudacion();

            // Assert
            Assert.AreEqual(0, orden.ContribuyenteId);
        }

        [TestMethod]
        [Description("Test 3: Obtener orden por ID")]
        public async Task ObtenerOrden_IdValido_RetornaOrden()
        {
            // Arrange
            var orden = new OrdenRecaudacion
            {
                NumeroOrden = "ORD-TEST-001",
                Total = 100m,
                Estado = "PENDIENTE",
                UsuarioCreacion = "test"
            };
            var id = await _ordenRepo.CrearAsync(orden);

            // Act
            var resultado = await _ordenRepo.ObtenerPorIdAsync(id);

            // Assert
            Assert.IsNotNull(resultado);
            Assert.AreEqual("ORD-TEST-001", resultado.NumeroOrden);
        }

        [TestMethod]
        [Description("Test 4: Actualizar estado de orden")]
        public async Task ActualizarEstado_OrdenExistente_Exitoso()
        {
            // Arrange
            var orden = new OrdenRecaudacion { Estado = "PENDIENTE", UsuarioCreacion = "test" };
            var id = await _ordenRepo.CrearAsync(orden);

            // Act
            var resultado = await _ordenRepo.ActualizarEstadoAsync(id, "PROCESADA", "admin");

            // Assert
            Assert.IsTrue(resultado);
            var ordenActualizada = await _ordenRepo.ObtenerPorIdAsync(id);
            Assert.AreEqual("PROCESADA", ordenActualizada.Estado);
        }

        [TestMethod]
        [Description("Test 5: Orden inexistente retorna null")]
        public async Task ObtenerOrden_IdInexistente_RetornaNull()
        {
            // Act
            var resultado = await _ordenRepo.ObtenerPorIdAsync(9999);

            // Assert
            Assert.IsNull(resultado);
        }

        [TestMethod]
        [Description("Test 6: Numero de orden conserva correlativo de solicitud GOP")]
        public void ConstruirNumeroOrdenDesdeNumeroSolicitud_UsaCorrelativoGop()
        {
            var numeroOrden = OrdenRecaudacionService.ConstruirNumeroOrdenDesdeNumeroSolicitud(
                "DGAC-GOP-2026-AOCR0015",
                2026);

            Assert.AreEqual("DGAC-OR-2026-AOCR015", numeroOrden);
        }
    }
}
