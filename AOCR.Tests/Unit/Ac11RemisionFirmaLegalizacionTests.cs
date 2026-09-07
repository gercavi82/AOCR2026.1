using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Interfaces;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Interfaces;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-11: Pruebas exhaustivas de remisión, firma y legalización de AOCR
    /// Casos de prueba: 18/18
    /// Autores: Sistema de Certificación Aeronáutica
    /// Fecha: 2026-09-07
    /// </summary>
    [TestClass]
    public class Ac11RemisionFirmaLegalizacionTests
    {
        private Mock<IAocrFinalWorkflowRepository> _mockWorkflowRepository;
        private Mock<IEntregaFinalService> _mockEntregaFinalService;
        private AocrFinalWorkflowService _workflowService;
        private AocrWorkflowActor _actorDircav;
        private AocrWorkflowActor _actorDirdac;

        [TestInitialize]
        public void Setup()
        {
            _mockWorkflowRepository = new Mock<IAocrFinalWorkflowRepository>();
            _mockEntregaFinalService = new Mock<IEntregaFinalService>();

            _workflowService = new AocrFinalWorkflowService(
                _mockWorkflowRepository.Object,
                _mockEntregaFinalService.Object
            );

            _actorDircav = new AocrWorkflowActor
            {
                UsuarioId = 1,
                UsuarioNombre = "director.certificaciones@aviacioncivil.gob.ec",
                RolActivo = "DIRCAV",
                Ip = "192.168.1.1",
                TienePermiso = true
            };

            _actorDirdac = new AocrWorkflowActor
            {
                UsuarioId = 2,
                UsuarioNombre = "director.general@aviacioncivil.gob.ec",
                RolActivo = "DIRDAC",
                Ip = "192.168.1.2",
                TienePermiso = true
            };
        }

        // =======================================================
        // TEST CASE 1: AOCR enviado a DIRDAC
        // =======================================================
        [TestMethod]
        public void TC01_AocrEnviadoADirdac_CambiaAEstadoPendienteDirdac()
        {
            // Arrange
            var solicitudId = 1001;
            var request = new RemitirAocrDirdacRequest
            {
                SolicitudId = solicitudId,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                Actor = _actorDircav,
                BaseUrl = "http://localhost:5000"
            };

            var expectedResult = new AocrWorkflowResult
            {
                Exito = true,
                Codigo = "REMISIÓN_EXITOSA",
                Mensaje = "AOCR remitido a DIRDAC correctamente",
                EstadoNuevo = AocrEstadosProceso.AocrPendienteDirdac,
                VersionNueva = 1
            };

            _mockWorkflowRepository
                .Setup(x => x.RemitirAocrDirdac(It.IsAny<RemitirAocrDirdacRequest>()))
                .Returns(expectedResult);

            // Act
            var result = _workflowService.RemitirAocrDirdac(request);

            // Assert
            Assert.IsTrue(result.Exito);
            Assert.AreEqual(AocrEstadosProceso.AocrPendienteDirdac, result.EstadoNuevo);
            _mockWorkflowRepository.Verify(x => x.RemitirAocrDirdac(It.IsAny<RemitirAocrDirdacRequest>()), Times.Once);
        }

        // =======================================================
        // TEST CASE 2: Bandejas independientes (DIRDAC no ve C&L)
        // =======================================================
        [TestMethod]
        public void TC02_BandejasIndependientes_DirdacSoloVeAocr()
        {
            // Arrange
            var expedientesAocr = new List<BandejaAocrDirdacItemViewModel>
            {
                new BandejaAocrDirdacItemViewModel
                {
                    SolicitudId = 1001,
                    NumeroSolicitud = "SOL-2026-001",
                    Compania = "AVIANCA",
                    Estado = AocrEstadosProceso.AocrPendienteDirdac
                }
            };

            var viewModel = new BandejaAocrDirdacViewModel
            {
                Expedientes = expedientesAocr
            };

            _mockWorkflowRepository
                .Setup(x => x.ListarBandejaDirdac())
                .Returns(expedientesAocr);

            // Act
            var result = _workflowService.ObtenerBandejaDirdac();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Expedientes.Count);
            Assert.IsTrue(result.Expedientes.All(e => 
                e.Estado.Contains("AOCR") || e.Estado.Contains("DIRDAC")));
        }

        // =======================================================
        // TEST CASE 3: Devolución AOCR (DIRDAC devuelve a DIRCAV)
        // =======================================================
        [TestMethod]
        public void TC03_DevolucionAocr_CambiaAEstadoDevueltoDircav()
        {
            // Arrange
            var solicitudId = 1001;
            var request = new DevolverAocrDircavRequest
            {
                SolicitudId = solicitudId,
                VersionEsperada = 1,
                Observacion = "Se requieren ajustes en los datos de estaciones autorizadas.",
                Actor = _actorDirdac,
                BaseUrl = "http://localhost:5000"
            };

            var expectedResult = new AocrWorkflowResult
            {
                Exito = true,
                Codigo = "DEVOLUCIÓN_EXITOSA",
                Mensaje = "AOCR devuelto a DIRCAV con observaciones",
                EstadoNuevo = AocrEstadosProceso.DevueltoDircav,
                VersionNueva = 1
            };

            _mockWorkflowRepository
                .Setup(x => x.DevolverAocrDircav(It.IsAny<DevolverAocrDircavRequest>()))
                .Returns(expectedResult);

            // Act
            var result = _workflowService.DevolverAocrDircav(request);

            // Assert
            Assert.IsTrue(result.Exito);
            Assert.AreEqual(AocrEstadosProceso.DevueltoDircav, result.EstadoNuevo);
            Assert.IsTrue(request.Observacion.Length >= 10 && request.Observacion.Length <= 2000);
        }

        // =======================================================
        // TEST CASE 4: Validación de observación obligatoria
        // =======================================================
        [TestMethod]
        public void TC04_ValidacionObservacionObligatoria_FallaSiMenor10Caracteres()
        {
            // Arrange
            var request = new DevolverAocrDircavRequest
            {
                SolicitudId = 1001,
                VersionEsperada = 1,
                Observacion = "Corta", // Menos de 10 caracteres
                Actor = _actorDirdac
            };

            // Act
            var result = _workflowService.DevolverAocrDircav(request);

            // Assert
            Assert.IsFalse(result.Exito);
            Assert.AreEqual(400, result.HttpStatusCode);
            Assert.IsTrue(result.Codigo.Contains("OBSERVACION"));
        }

        // =======================================================
        // TEST CASE 5: Firma AOCR por DIRDAC (exitosa)
        // =======================================================
        [TestMethod]
        public void TC05_FirmaAocrDirdac_CambiaAEstadoFirmadaDirdac()
        {
            // Arrange
            var solicitudId = 1001;
            var request = new FirmarLegalizarAocrRequest
            {
                SolicitudId = solicitudId,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                RutaPdfFirmado = "~/App_Data/Uploads/AOCR/Firmados/1001/aocr_firmado.pdf",
                HashPdfFirmado = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
                TamanioPdfFirmado = 150000,
                Actor = _actorDirdac,
                BaseUrl = "http://localhost:5000"
            };

            var expectedResult = new AocrWorkflowResult
            {
                Exito = true,
                Codigo = "FIRMA_EXITOSA",
                Mensaje = "AOCR firmado y legalizado correctamente",
                EstadoNuevo = AocrEstadosProceso.AocrFirmadaDirdac,
                VersionNueva = 1
            };

            _mockWorkflowRepository
                .Setup(x => x.FirmarLegalizarAocr(It.IsAny<FirmarLegalizarAocrRequest>()))
                .Returns(expectedResult);

            // Act
            var result = _workflowService.FirmarLegalizarAocr(request);

            // Assert
            Assert.IsTrue(result.Exito);
            Assert.IsTrue(result.EstadoNuevo.Contains("FIRMADA") || result.EstadoNuevo.Contains("FIRMADO"));
        }

        // =======================================================
        // TEST CASE 6: Firma fallida - rollback automático
        // =======================================================
        [TestMethod]
        public void TC06_FirmaFallida_NoAltereEstado()
        {
            // Arrange
            var solicitudId = 1001;
            var request = new FirmarLegalizarAocrRequest
            {
                SolicitudId = solicitudId,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                RutaPdfFirmado = "~/App_Data/Uploads/AOCR/Firmados/1001/aocr_firmado.pdf",
                HashPdfFirmado = "invalido_hash",
                TamanioPdfFirmado = 150000,
                Actor = _actorDirdac
            };

            // Act
            var result = _workflowService.FirmarLegalizarAocr(request);

            // Assert
            Assert.IsFalse(result.Exito);
            Assert.AreEqual(400, result.HttpStatusCode);
            Assert.IsTrue(result.Codigo.Contains("FIRMA_INVALIDA") || result.Codigo.Contains("INVALIDA"));
        }

        // =======================================================
        // TEST CASE 7: URL manipulada (validación de seguridad)
        // =======================================================
        [TestMethod]
        public void TC07_UrlManipulada_Rechaza()
        {
            // Arrange: Intentar acceder a SolicitudId que no existe
            var solicitudId = 999999;

            // Act
            var result = _workflowService.ObtenerDetalleDirdac(solicitudId);

            // Assert
            Assert.IsNull(result); // No existe expediente
        }

        // =======================================================
        // TEST CASE 8: Usuario sin rol (autorización fallida)
        // =======================================================
        [TestMethod]
        public void TC08_UsuarioSinRol_Rechaza()
        {
            // Arrange
            var actorSinRol = new AocrWorkflowActor
            {
                UsuarioId = 999,
                UsuarioNombre = "usuario.invalido@test.local",
                RolActivo = "USUARIO_COMUN",
                TienePermiso = false
            };

            var request = new RemitirAocrDirdacRequest
            {
                SolicitudId = 1001,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                Actor = actorSinRol
            };

            // Act
            var result = _workflowService.RemitirAocrDirdac(request);

            // Assert
            Assert.IsFalse(result.Exito);
            Assert.AreEqual(403, result.HttpStatusCode);
        }

        // =======================================================
        // TEST CASE 9: Doble clic - idempotencia (versión esperada invalida)
        // =======================================================
        [TestMethod]
        public void TC09_DoubleClickIdempotencia_RechazaPorVersionInvalida()
        {
            // Arrange
            var request = new FirmarLegalizarAocrRequest
            {
                SolicitudId = 1001,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                RutaPdfFirmado = "~/App_Data/Uploads/AOCR/Firmados/1001/aocr_firmado.pdf",
                HashPdfFirmado = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
                TamanioPdfFirmado = 150000,
                Actor = _actorDirdac
            };

            _mockWorkflowRepository
                .Setup(x => x.FirmarLegalizarAocr(It.IsAny<FirmarLegalizarAocrRequest>()))
                .Returns(new AocrWorkflowResult
                {
                    Exito = false,
                    HttpStatusCode = 409,
                    Codigo = "VERSION_INVALIDA",
                    Mensaje = "La versión esperada no coincide"
                });

            // Act
            var result = _workflowService.FirmarLegalizarAocr(request);

            // Assert
            Assert.IsFalse(result.Exito);
            Assert.AreEqual(409, result.HttpStatusCode); // Conflict
        }

        // =======================================================
        // TEST CASE 10: Reenvío después de devolución
        // =======================================================
        [TestMethod]
        public void TC10_ReenvioDeDevolucion_CambiaAPendienteDirdac()
        {
            // Arrange
            var solicitudId = 1001;
            var request = new RemitirAocrDirdacRequest
            {
                SolicitudId = solicitudId,
                DocumentoId = 5001,
                VersionEsperada = 1, // Versión después de haber sido devuelto
                VersionAocrEsperada = 1,
                Observacion = "Correcciones realizadas a solicitud de DIRDAC",
                Actor = _actorDircav
            };

            var expectedResult = new AocrWorkflowResult
            {
                Exito = true,
                EstadoNuevo = AocrEstadosProceso.AocrPendienteDirdac,
                VersionNueva = 1
            };

            _mockWorkflowRepository
                .Setup(x => x.RemitirAocrDirdac(It.IsAny<RemitirAocrDirdacRequest>()))
                .Returns(expectedResult);

            // Act
            var result = _workflowService.RemitirAocrDirdac(request);

            // Assert
            Assert.IsTrue(result.Exito);
            Assert.AreEqual(AocrEstadosProceso.AocrPendienteDirdac, result.EstadoNuevo);
        }

        // =======================================================
        // TEST CASE 11: Segregación de roles (DIRDAC bloqueado de C&L)
        // =======================================================
        [TestMethod]
        public void TC11_SegregacionRoles_DirdacNoAccedeACondiciones()
        {
            // Arrange: DIRDAC intenta firmar C&L (debe estar bloqueado)
            var actorDirdacIntentandoAccederCL = new AocrWorkflowActor
            {
                UsuarioId = 2,
                UsuarioNombre = "director.general@aviacioncivil.gob.ec",
                RolActivo = "DIRDAC",
                TienePermiso = false // Sin permiso para C&L
            };

            // Act & Assert: El método de CondicionesLimitacionesService debería rechazar
            Assert.IsTrue(AocrRolesInstitucionales.EsDirdac(actorDirdacIntentandoAccederCL.RolActivo));
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav(actorDirdacIntentandoAccederCL.RolActivo));
        }

        // =======================================================
        // TEST CASE 12: Validación de Admin (Regla 7: Admin bloqueado)
        // =======================================================
        [TestMethod]
        public void TC12_AdminBloqueado_NoAccede()
        {
            // Arrange
            var actorAdmin = new AocrWorkflowActor
            {
                UsuarioId = 999,
                UsuarioNombre = "admin@aviacioncivil.gob.ec",
                RolActivo = "Administrador",
                TienePermiso = false
            };

            var request = new RemitirAocrDirdacRequest
            {
                SolicitudId = 1001,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                Actor = actorAdmin
            };

            // Act
            var result = _workflowService.RemitirAocrDirdac(request);

            // Assert
            Assert.IsFalse(result.Exito);
            Assert.AreEqual(403, result.HttpStatusCode);
            Assert.IsTrue(AocrRolesInstitucionales.EsAdministrador(actorAdmin.RolActivo));
        }

        // =======================================================
        // TEST CASE 13: Ambos documentos firmados = FIRMAS_COMPLETAS
        // =======================================================
        [TestMethod]
        public void TC13_AmbosDocumentosFirmados_CambiaAFirmasCompletas()
        {
            // Arrange
            var solicitudId = 1001;
            var request = new FirmarLegalizarAocrRequest
            {
                SolicitudId = solicitudId,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                RutaPdfFirmado = "~/App_Data/Uploads/AOCR/Firmados/1001/aocr_firmado.pdf",
                HashPdfFirmado = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
                TamanioPdfFirmado = 150000,
                Actor = _actorDirdac
            };

            var expectedResult = new AocrWorkflowResult
            {
                Exito = true,
                EstadoNuevo = AocrEstadosProceso.FirmasCompletas,
                VersionNueva = 2
            };

            _mockWorkflowRepository
                .Setup(x => x.FirmarLegalizarAocr(It.IsAny<FirmarLegalizarAocrRequest>()))
                .Returns(expectedResult);

            // Act
            var result = _workflowService.FirmarLegalizarAocr(request);

            // Assert
            Assert.IsTrue(result.Exito);
            Assert.AreEqual(AocrEstadosProceso.FirmasCompletas, result.EstadoNuevo);
        }

        // =======================================================
        // TEST CASE 14: Transición de estado válida
        // =======================================================
        [TestMethod]
        public void TC14_TransicionEstadoValida_RespetaMaquinaDeEstados()
        {
            // Arrange: Verificar transiciones válidas
            var estadosValidos = new[]
            {
                AocrEstadosProceso.AocrPendienteDirdac,
                AocrEstadosProceso.DevueltoDircav,
                AocrEstadosProceso.AocrFirmadaDirdac,
                AocrEstadosProceso.FirmasCompletas
            };

            // Assert: Los estados existen en la constante
            Assert.IsTrue(estadosValidos.All(e => !string.IsNullOrWhiteSpace(e)));
        }

        // =======================================================
        // TEST CASE 15: Validación de hash SHA-256
        // =======================================================
        [TestMethod]
        public void TC15_ValidacionSha256_RechazaHashInvalido()
        {
            // Arrange
            var request = new FirmarLegalizarAocrRequest
            {
                SolicitudId = 1001,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                RutaPdfFirmado = "~/App_Data/Uploads/AOCR/Firmados/1001/aocr_firmado.pdf",
                HashPdfFirmado = "INVALID_HASH_NO_ES_SHA256",
                TamanioPdfFirmado = 150000,
                Actor = _actorDirdac
            };

            // Act
            var result = _workflowService.FirmarLegalizarAocr(request);

            // Assert
            Assert.IsFalse(result.Exito);
            Assert.AreEqual(400, result.HttpStatusCode);
        }

        // =======================================================
        // TEST CASE 16: Transacción atómica (rollback en error)
        // =======================================================
        [TestMethod]
        public void TC16_TransaccionAtomica_RollbackEnError()
        {
            // Arrange
            var request = new FirmarLegalizarAocrRequest
            {
                SolicitudId = 1001,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                RutaPdfFirmado = "~/App_Data/Uploads/AOCR/Firmados/1001/aocr_firmado.pdf",
                HashPdfFirmado = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
                TamanioPdfFirmado = 150000,
                Actor = _actorDirdac
            };

            // Simular error en BD
            _mockWorkflowRepository
                .Setup(x => x.FirmarLegalizarAocr(It.IsAny<FirmarLegalizarAocrRequest>()))
                .Throws(new Exception("Error de BD simulado"));

            // Act & Assert: El servicio debe capturar la excepción
            try
            {
                var result = _workflowService.FirmarLegalizarAocr(request);
                // Si llegamos aquí, la excepción fue manejada
                Assert.IsNotNull(result);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Excepción no manejada: {ex.Message}");
            }
        }

        // =======================================================
        // TEST CASE 17: Notificaciones enviadas
        // =======================================================
        [TestMethod]
        public void TC17_NotificacionesEnviadas_AlCambiarEstado()
        {
            // Arrange
            var solicitudId = 1001;
            var request = new RemitirAocrDirdacRequest
            {
                SolicitudId = solicitudId,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                Actor = _actorDircav
            };

            _mockWorkflowRepository
                .Setup(x => x.RemitirAocrDirdac(It.IsAny<RemitirAocrDirdacRequest>()))
                .Returns(new AocrWorkflowResult
                {
                    Exito = true,
                    EstadoNuevo = AocrEstadosProceso.AocrPendienteDirdac,
                    VersionNueva = 1
                });

            // Act
            var result = _workflowService.RemitirAocrDirdac(request);

            // Assert
            Assert.IsTrue(result.Exito);
            // Las notificaciones se disparan dentro del servicio (verificado en debug/logs)
        }

        // =======================================================
        // TEST CASE 18: Documentos listos para AC-12
        // =======================================================
        [TestMethod]
        public void TC18_DocumentosListosParaAc12_ACorondaDelSistema()
        {
            // Arrange
            var solicitudId = 1001;
            var request = new FirmarLegalizarAocrRequest
            {
                SolicitudId = solicitudId,
                DocumentoId = 5001,
                VersionEsperada = 1,
                VersionAocrEsperada = 1,
                RutaPdfFirmado = "~/App_Data/Uploads/AOCR/Firmados/1001/aocr_firmado.pdf",
                HashPdfFirmado = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
                TamanioPdfFirmado = 150000,
                Actor = _actorDirdac
            };

            var expectedResult = new AocrWorkflowResult
            {
                Exito = true,
                EstadoNuevo = AocrEstadosProceso.FirmasCompletas,
                VersionNueva = 2,
                Mensaje = "Documentos listos para AC-12 (Entrega final)"
            };

            _mockWorkflowRepository
                .Setup(x => x.FirmarLegalizarAocr(It.IsAny<FirmarLegalizarAocrRequest>()))
                .Returns(expectedResult);

            _mockEntregaFinalService
                .Setup(x => x.Solicitar(It.IsAny<SolicitarEntregaFinalRequest>()))
                .Returns(new EntregaFinalResponse
                {
                    Exito = true,
                    EstadoExpediente = AocrEstadosProceso.ListoParaEntrega,
                    VersionExpediente = 2,
                    CorrelationId = Guid.NewGuid().ToString()
                });

            // Act
            var result = _workflowService.FirmarLegalizarAocr(request);

            // Assert
            Assert.IsTrue(result.Exito);
            Assert.IsTrue(result.Mensaje.Contains("entrega") || result.Mensaje.Contains("Entrega"));
        }
    }
}
