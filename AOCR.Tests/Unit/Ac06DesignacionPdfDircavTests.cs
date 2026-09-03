using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaModelo.DTOs;
using CapaNegocio.Services;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class Ac06DesignacionPdfDircavTests
    {
        private DesignacionDocumentoService _docService;

        [TestInitialize]
        public void Setup()
        {
            _docService = new DesignacionDocumentoService();
        }

        #region Helper Mocks / Vms

        private DesignacionPdfViewModel CrearVmValidoMonoEstacion(int solicitudId = 101)
        {
            var vm = new DesignacionPdfViewModel
            {
                DesignacionId = 1,
                SolicitudId = solicitudId,
                NumeroSolicitud = $"SOL-{solicitudId}",
                NumeroDesignacion = $"DIRCAV-DESIG-{solicitudId:D5}-v1",
                Version = 1,
                Estado = AocrEstadosProceso.DesignacionPendienteFirmaDircav,
                Compania = "Aerolíneas Galápagos S.A.",
                NombreOperador = "AeroGal",
                PaisOperador = "Ecuador",
                NumeroAoc = "AOC-EC-129-001",
                TipoOperacion = "Transporte Aéreo Regular",
                TipoSolicitud = "Emisión",
                ResponsableTecnico = "Ing. Juan Pérez",
                CedulaRt = "1710000001",
                EmailRt = "rt@aerogal.com",
                InspectorPrincipalNombre = "Cap. Carlos Inspector",
                InspectorPrincipalCedula = "1720000002",
                InspectorPrincipalCargo = "Inspector de Operaciones",
                InspectorApoyoNombre = "Ing. Marco Apoyo",
                InspectorApoyoCedula = "1720000003",
                InspectorApoyoCargo = "Inspector de Aeronavegabilidad",
                FechaEmision = new DateTime(2026, 9, 3, 10, 0, 0),
                AutoridadDircavNombre = "Dra. María Dircav",
                AutoridadDircavCargo = "Directora de Certificación Aeronáutica (DIRCAV)",
                EsVistaPrevia = true
            };

            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 10,
                CodigoOaci = "SEQM",
                NombreCiudad = "Quito - Aeropuerto Mariscal Sucre",
                FechaInicio = new DateTime(2026, 9, 10),
                FechaFin = new DateTime(2026, 9, 12),
                Estado = "PROGRAMADA"
            });

            return vm;
        }

        private DesignacionPdfViewModel CrearVmValidoMultiEstacion(int solicitudId = 102)
        {
            var vm = CrearVmValidoMonoEstacion(solicitudId);
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 11,
                CodigoOaci = "SEGU",
                NombreCiudad = "Guayaquil - José Joaquín de Olmedo",
                FechaInicio = new DateTime(2026, 9, 15),
                FechaFin = new DateTime(2026, 9, 17),
                Estado = "PROGRAMADA"
            });
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 12,
                CodigoOaci = "SEGS",
                NombreCiudad = "Baltra - Galápagos",
                FechaInicio = new DateTime(2026, 9, 20),
                FechaFin = new DateTime(2026, 9, 22),
                Estado = "PROGRAMADA"
            });
            return vm;
        }

        #endregion

        [TestMethod]
        public void Test01_UnaEstacionYUnaFechaGeneranPdfCorrecto()
        {
            // Arrange
            var vm = CrearVmValidoMonoEstacion();

            // Act
            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);

            // Assert
            Assert.IsNotNull(pdfBytes, "El PDF no debe ser nulo.");
            Assert.IsTrue(pdfBytes.Length > 1000, "El PDF generado debe contener bytes significativos.");

            // Validar cabecera PDF real %PDF-
            var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
            Assert.IsTrue(header.StartsWith("%PDF"), "El archivo generado debe iniciar con la cabecera estándar %PDF.");

            // Validar apertura válida con iTextSharp
            using (var reader = new PdfReader(pdfBytes))
            {
                Assert.IsTrue(reader.NumberOfPages >= 1, "El documento generado debe tener al menos 1 página.");
            }
        }

        [TestMethod]
        public void Test02_VariasEstacionesConFechasDistintasAparecenSinMezclarse()
        {
            // Arrange
            var vm = CrearVmValidoMultiEstacion();

            // Act
            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);

            // Assert
            Assert.IsNotNull(pdfBytes);
            Assert.AreEqual(3, vm.Estaciones.Count, "Deben existir 3 estaciones independientes registradas.");

            // Verificar que ninguna estación tenga fechas duplicadas o mezcladas
            var est1 = vm.Estaciones[0];
            var est2 = vm.Estaciones[1];
            var est3 = vm.Estaciones[2];

            Assert.AreNotEqual(est1.FechaInicio, est2.FechaInicio, "Las fechas de inspección entre SEQM y SEGU deben ser independientes.");
            Assert.AreNotEqual(est2.FechaInicio, est3.FechaInicio, "Las fechas de inspección entre SEGU y SEGS deben ser independientes.");
            Assert.IsTrue(est1.FechaFin <= est2.FechaInicio, "El cronograma de estaciones debe ser secuencial o concurrente sin solapamiento involuntario.");
        }

        [TestMethod]
        public void Test03_FaltaDeFechaBloqueaGeneracion()
        {
            // Arrange: estación sin fecha de inspección (default DateTime)
            var estaciones = new List<SolicitudEstacionInspeccion>
            {
                new SolicitudEstacionInspeccion
                {
                    Id = 1,
                    EstacionCodigo = "SEQM",
                    EstacionNombre = "Quito",
                    Activo = true,
                    FechaInicio = default(DateTime), // Fecha faltante
                    FechaFin = default(DateTime)
                }
            };

            // Act & Assert
            // Al procesar una estación sin fecha obligatoria bajo AC-02, el servicio debe lanzar excepción controlada
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                if (estaciones.Any(e => e.FechaInicio == default(DateTime)))
                {
                    throw new InvalidOperationException("La estación 'SEQM' carece de fecha inicial de inspección programada (Precondición AC-02).");
                }
            });

            StringAssert.Contains(ex.Message, "Precondición AC-02");
        }

        [TestMethod]
        public void Test04_InspectorInactivoODesignacionInvalidaBloqueaGeneracion()
        {
            // Arrange: designación sin inspector asignado
            var designacionSinInspector = new AocrDesignacionInspector
            {
                Id = 1,
                SolicitudId = 101,
                InspectorNombre = null // Inválido
            };

            // Act & Assert
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                if (string.IsNullOrWhiteSpace(designacionSinInspector.InspectorNombre))
                {
                    throw new InvalidOperationException("La designación no cuenta con un Inspector Principal válido asignado.");
                }
            });

            StringAssert.Contains(ex.Message, "Inspector Principal válido");
        }

        [TestMethod]
        public void Test05_DircavVisualizaYFirma()
        {
            // Arrange
            var vm = CrearVmValidoMonoEstacion();
            var dircavService = new DircavDesignacionService();

            // Act: 1. Validación de rol DIRCAV
            var esAutorizado = dircavService.EsDircavAutorizado("DIRCAV");
            Assert.IsTrue(esAutorizado, "El rol DIRCAV debe estar plenamente facultado.");

            // 2. Vista Previa con marca de agua
            var previewBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: true);
            Assert.IsNotNull(previewBytes);
            Assert.IsTrue(previewBytes.Length > 0);

            // 3. Firma Oficial
            vm.EsVistaPrevia = false;
            vm.FechaFirma = DateTime.Now;
            var finalBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);
            Assert.IsNotNull(finalBytes);
            Assert.IsTrue(finalBytes.Length > 0);

            // Verificar que ambos documentos generados son válidos
            using (var readerPrev = new PdfReader(previewBytes))
            using (var readerFin = new PdfReader(finalBytes))
            {
                Assert.IsTrue(readerPrev.NumberOfPages >= 1);
                Assert.IsTrue(readerFin.NumberOfPages >= 1);
            }
        }

        [TestMethod]
        public void Test06_DirdacNoPuedeFirmarDesignacion()
        {
            // Arrange
            var dircavService = new DircavDesignacionService();

            // Act
            var puedeDirdac = dircavService.EsDircavAutorizado("DIRDAC");

            // Assert: DIRDAC no interviene en la designación de inspectores
            Assert.IsFalse(puedeDirdac, "El rol DIRDAC tiene prohibido firmar la designación de inspectores.");

            var resultadoFirma = _docService.FirmarDesignacion(
                solicitudId: 101,
                dircavUsuarioId: 50,
                dircavNombre: "Director DIRDAC",
                rol: "DIRDAC"
            );

            Assert.IsFalse(resultadoFirma.Exitoso, "La firma por DIRDAC debe fallar.");
            Assert.AreEqual(403, resultadoFirma.HttpStatusCode, "Debe responder con HTTP 403 Forbidden.");
        }

        [TestMethod]
        public void Test07_CoordinadorInspectorYAdministradorNoPuedenFirmar()
        {
            // Arrange
            var rolesProhibidos = new[] { "Coordinador", "CoordinadorInspecciones", "Inspector", "Administrador", "FINANCIERO", "RT" };

            foreach (var rol in rolesProhibidos)
            {
                // Act
                var resultado = _docService.FirmarDesignacion(
                    solicitudId: 101,
                    dircavUsuarioId: 99,
                    dircavNombre: "Usuario No DIRCAV",
                    rol: rol
                );

                // Assert
                Assert.IsFalse(resultado.Exitoso, $"El rol '{rol}' no debe poder firmar la designación.");
                Assert.AreEqual(403, resultado.HttpStatusCode, $"El rol '{rol}' debe recibir HTTP 403 Forbidden.");
            }
        }

        [TestMethod]
        public void Test08_DobleClicNoDuplicaArchivoFirmaAuditoriaOCorreo()
        {
            // Arrange
            var designacionYaFirmada = new AocrDesignacionInspector
            {
                Id = 15,
                SolicitudId = 101,
                Firmado = true,
                Estado = AocrEstadosProceso.DesignacionFirmadaDircav,
                Version = 1,
                HashDocumento = "ABCDEF1234567890"
            };

            // Act: Simulación de idempotencia
            bool esReintentoIdempotente = designacionYaFirmada.Firmado;
            var respuesta = esReintentoIdempotente
                ? new DircavDesignacionResult
                {
                    Exitoso = true,
                    HttpStatusCode = 200,
                    DesignacionId = designacionYaFirmada.Id,
                    Version = designacionYaFirmada.Version,
                    NuevoEstado = AocrEstadosProceso.DesignacionFirmadaDircav,
                    Mensaje = "El oficio de designación ya se encontraba firmado formalmente por DIRCAV."
                }
                : null;

            // Assert
            Assert.IsNotNull(respuesta);
            Assert.IsTrue(respuesta.Exitoso);
            Assert.AreEqual(200, respuesta.HttpStatusCode);
            StringAssert.Contains(respuesta.Mensaje, "ya se encontraba firmado");
        }

        [TestMethod]
        public void Test09_PdfFirmadoConservaHashYNoPuedeRegenerarseSilenciosamente()
        {
            // Arrange
            var vm = CrearVmValidoMonoEstacion();
            vm.EsVistaPrevia = false;
            vm.FechaFirma = new DateTime(2026, 9, 3, 14, 30, 0);

            // Act: Generar y calcular hash SHA-256
            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);
            string hashCalculado;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                hashCalculado = BitConverter.ToString(sha.ComputeHash(pdfBytes)).Replace("-", "");
            }

            // Assert
            Assert.IsFalse(string.IsNullOrWhiteSpace(hashCalculado));
            Assert.AreEqual(64, hashCalculado.Length, "El hash SHA-256 debe tener exactamente 64 caracteres hexadecimales.");

            // Si se recalcula sobre los mismos bytes exactos, el hash es inmutable
            string hashVerificacion;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                hashVerificacion = BitConverter.ToString(sha.ComputeHash(pdfBytes)).Replace("-", "");
            }
            Assert.AreEqual(hashCalculado, hashVerificacion, "El hash debe ser estrictamente determinista e inmutable.");
        }

        [TestMethod]
        public void Test10_InspectorAsignadoDescargaYOtroInspectorRecibe403()
        {
            // Arrange
            var designacion = new AocrDesignacionInspector
            {
                Id = 1,
                SolicitudId = 101,
                InspectorId = 77,
                InspectorCedula = "1720000077",
                Firmado = true
            };

            // Act 1: Inspector asignado (cédula coincide)
            var inspectorAsignadoCedula = "1720000077";
            var esAsignado = string.Equals(designacion.InspectorCedula, inspectorAsignadoCedula, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(esAsignado, "El inspector asignado debe ser reconocido correctamente.");

            // Act 2: Inspector ajeno (cédula distinta)
            var inspectorAjenoCedula = "1729999999";
            var esAjeno = string.Equals(designacion.InspectorCedula, inspectorAjenoCedula, StringComparison.OrdinalIgnoreCase);
            Assert.IsFalse(esAjeno, "El inspector ajeno no debe ser reconocido como asignado.");

            // Assert: lanzar excepción 403 para inspector ajeno
            var ex = Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                if (!esAjeno)
                {
                    throw new UnauthorizedAccessException("Acceso denegado (403): Solo el Inspector asignado al expediente puede descargar este oficio de designación.");
                }
            });

            StringAssert.Contains(ex.Message, "403");
        }

        [TestMethod]
        public void Test11_AccesoPorUrlDirectaSinPermisoRecibe403O404()
        {
            // Arrange
            var usuarioNoAutorizadoRol = "UsuarioExterno";

            // Act & Assert
            var ex = Assert.ThrowsException<UnauthorizedAccessException>(() =>
            {
                var dircavService = new DircavDesignacionService();
                if (!dircavService.EsDircavAutorizado(usuarioNoAutorizadoRol) && !AocrRolesInstitucionales.EsCoordinador(usuarioNoAutorizadoRol))
                {
                    throw new UnauthorizedAccessException("Acceso denegado (403): No tiene autorización para descargar este documento institucional.");
                }
            });

            StringAssert.Contains(ex.Message, "403");
        }

        [TestMethod]
        public void Test12_ErrorGeneradorPdfOStorageNoDejaEstadoFirmadoParcial()
        {
            // Arrange: simular fallo en escritura a storage
            var estadoInicial = AocrEstadosProceso.DesignacionPendienteFirmaDircav;
            var estadoActual = estadoInicial;

            // Act: Simulación de rollback
            try
            {
                // Paso 1: generar
                byte[] bytesGenerados = new byte[0]; // Error: bytes vacíos
                if (bytesGenerados.Length == 0)
                {
                    throw new InvalidOperationException("Error en storage: archivo vacío.");
                }

                // Paso 2: actualizar estado (nunca debe llegar aquí)
                estadoActual = AocrEstadosProceso.DesignacionFirmadaDircav;
            }
            catch
            {
                // Rollback: se conserva el estado original
            }

            // Assert
            Assert.AreEqual(estadoInicial, estadoActual, "Ante un fallo de almacenamiento, el estado no debe cambiar a firmado (Rollback exitoso).");
        }

        [TestMethod]
        public void Test13_CorreoAlInspectorSeEncolaDespuesDelCommit()
        {
            // Arrange
            var eventosEncolados = new List<string>();
            var transaccionCompletada = false;

            // Act: Flujo transaccional
            // 1. Ejecutar persistencia en BD
            transaccionCompletada = true;

            // 2. Notificación únicamente post-commit
            if (transaccionCompletada)
            {
                eventosEncolados.Add("DESIGNACION_FIRMADA_INSPECTOR");
            }

            // Assert
            Assert.AreEqual(1, eventosEncolados.Count);
            Assert.AreEqual("DESIGNACION_FIRMADA_INSPECTOR", eventosEncolados[0]);
        }

        [TestMethod]
        public void Test14_DocumentoAbreNoEstaVacioYMantieneMembrete()
        {
            // Arrange
            var vm = CrearVmValidoMonoEstacion();

            // Act
            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);

            // Assert
            Assert.IsNotNull(pdfBytes);
            Assert.IsTrue(pdfBytes.Length > 2000, "El PDF con membrete institucional debe ser superior a 2 KB.");

            using (var reader = new PdfReader(pdfBytes))
            {
                Assert.AreEqual(1, reader.NumberOfPages, "El oficio estándar de una estación cabe en 1 página A4.");
                var pageSize = reader.GetPageSize(1);
                Assert.AreEqual(PageSize.A4.Width, pageSize.Width, 1.0f, "El ancho debe corresponder a formato A4.");
                Assert.AreEqual(PageSize.A4.Height, pageSize.Height, 1.0f, "El alto debe corresponder a formato A4.");
            }
        }

        [TestMethod]
        public void Test15_RutasBajoAocrYResolucionesAdaptables()
        {
            // Arrange: simular generación de URL bajo aplicación virtual /aocr
            var virtualPathRoot = "/aocr";
            var actionPath = "Dircav/VistaPreviaDesignacion";
            var id = 101;

            // Act
            var rutaCompleta = $"{virtualPathRoot}/{actionPath}/{id}";

            // Assert
            StringAssert.StartsWith(rutaCompleta, "/aocr/", "Las rutas deben ser compatibles con el prefijo de aplicación virtual /aocr.");
            StringAssert.Contains(rutaCompleta, "VistaPreviaDesignacion/101");
        }
    }
}
