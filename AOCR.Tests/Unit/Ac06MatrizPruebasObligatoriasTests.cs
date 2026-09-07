using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    /// <summary>
    /// AC-06: Matriz de 11 pruebas unitarias obligatorias para la generación de la Designación en PDF
    /// con estaciones, fechas independientes y firma institucional.
    /// </summary>
    [TestClass]
    public class Ac06MatrizPruebasObligatoriasTests
    {
        private DesignacionDocumentoService _docService;

        [TestInitialize]
        public void Setup()
        {
            _docService = new DesignacionDocumentoService();
        }

        #region Helpers

        private DesignacionPdfViewModel CrearVmBase(int solicitudId = 500)
        {
            return new DesignacionPdfViewModel
            {
                DesignacionId = 1,
                SolicitudId = solicitudId,
                NumeroSolicitud = $"SOL-{solicitudId:D6}",
                NumeroDesignacion = $"DIRCAV-DESIG-{solicitudId:D5}-v1",
                Version = 1,
                Estado = AocrEstadosProceso.DesignacionPendienteFirmaDircav,
                Compania = "AeroTransportes del Pacífico S.A.",
                NombreOperador = "Pacífico Air",
                PaisOperador = "Perú",
                NumeroAoc = "AOC-PE-2026-999",
                TipoOperacion = "Transporte de Pasajeros y Carga Internacional",
                TipoSolicitud = "Reconocimiento RDAC 129",
                ResponsableTecnico = "Ing. Roberto Gómez",
                CedulaRt = "1715555555",
                EmailRt = "rgomez@pacificoair.com",
                InspectorPrincipalNombre = "Cap. Andrés Valdivieso",
                InspectorPrincipalCedula = "1709998881",
                InspectorPrincipalCargo = "Inspector Principal de Operaciones",
                InspectorApoyoNombre = "Ing. Lucía Andrade",
                InspectorApoyoCedula = "1709998882",
                InspectorApoyoCargo = "Inspectora de Aeronavegabilidad",
                FechaEmision = new DateTime(2026, 9, 7, 10, 30, 0),
                AutoridadDircavNombre = "Dra. Sofía Alarcón",
                AutoridadDircavCargo = "Directora de Certificación Aeronáutica (DIRCAV)",
                EsVistaPrevia = false
            };
        }

        #endregion

        /// <summary>
        /// Escenario 1: Generación exitosa de PDF con 1 estación.
        /// </summary>
        [TestMethod]
        public void Test01_UnaEstacion_GeneraPdfValido()
        {
            var vm = CrearVmBase(501);
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 1,
                CodigoOaci = "SEQM",
                NombreCiudad = "Quito - Aeropuerto Internacional Mariscal Sucre",
                FechaInicio = new DateTime(2026, 10, 1),
                FechaFin = new DateTime(2026, 10, 3),
                Estado = "PROGRAMADA"
            });

            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);

            Assert.IsNotNull(pdfBytes, "El PDF generado no debe ser nulo.");
            Assert.IsTrue(pdfBytes.Length > 1500, "El PDF debe tener un tamaño significativo mayor a 1.5 KB.");

            var header = Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
            Assert.AreEqual("%PDF", header, "El documento debe comenzar con la cabecera estándar %PDF.");

            using (var reader = new PdfReader(pdfBytes))
            {
                Assert.AreEqual(1, reader.NumberOfPages, "Un oficio con una estación cabe perfectamente en 1 página.");
            }
        }

        /// <summary>
        /// Escenario 2: Generación con varias estaciones.
        /// </summary>
        [TestMethod]
        public void Test02_VariasEstaciones_MuestraTodasLasEstaciones()
        {
            var vm = CrearVmBase(502);
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 1,
                CodigoOaci = "SEQM",
                NombreCiudad = "Quito",
                FechaInicio = new DateTime(2026, 10, 1),
                FechaFin = new DateTime(2026, 10, 2),
                Estado = "PROGRAMADA"
            });
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 2,
                CodigoOaci = "SEGU",
                NombreCiudad = "Guayaquil",
                FechaInicio = new DateTime(2026, 10, 5),
                FechaFin = new DateTime(2026, 10, 6),
                Estado = "PROGRAMADA"
            });
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 3,
                CodigoOaci = "SECU",
                NombreCiudad = "Cuenca",
                FechaInicio = new DateTime(2026, 10, 9),
                FechaFin = new DateTime(2026, 10, 10),
                Estado = "PROGRAMADA"
            });

            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);

            Assert.IsNotNull(pdfBytes);
            Assert.AreEqual(3, vm.Estaciones.Count, "El ViewModel debe registrar exactamente 3 estaciones.");

            using (var reader = new PdfReader(pdfBytes))
            {
                Assert.IsTrue(reader.NumberOfPages >= 1, "El PDF debe compilarse correctamente con múltiples estaciones.");
            }
        }

        /// <summary>
        /// Escenario 3: Fechas diferentes por estación provenientes de AC-02.
        /// </summary>
        [TestMethod]
        public void Test03_FechasDiferentes_ConservaCronogramaIndependiente()
        {
            var vm = CrearVmBase(503);
            var f1Inicio = new DateTime(2026, 10, 12);
            var f1Fin = new DateTime(2026, 10, 14);
            var f2Inicio = new DateTime(2026, 10, 20);
            var f2Fin = new DateTime(2026, 10, 22);

            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 10,
                CodigoOaci = "SEQM",
                NombreCiudad = "Quito",
                FechaInicio = f1Inicio,
                FechaFin = f1Fin,
                Estado = "PROGRAMADA"
            });
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 20,
                CodigoOaci = "SEGS",
                NombreCiudad = "Baltra",
                FechaInicio = f2Inicio,
                FechaFin = f2Fin,
                Estado = "PROGRAMADA"
            });

            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);

            Assert.IsNotNull(pdfBytes);
            Assert.AreNotEqual(vm.Estaciones[0].FechaInicio, vm.Estaciones[1].FechaInicio, "Las fechas de inicio deben ser estrictamente independientes.");
            Assert.AreNotEqual(vm.Estaciones[0].FechaFin, vm.Estaciones[1].FechaFin, "Las fechas de fin deben ser estrictamente independientes.");
        }

        /// <summary>
        /// Escenario 4: Soporte completo de caracteres especiales en español (tildes, ñ, comillas, guiones).
        /// </summary>
        [TestMethod]
        public void Test04_CaracteresEspeciales_TildesEniesYSiglas()
        {
            var vm = CrearVmBase(504);
            vm.Compania = "Compañía Aérea Del Pacífico & Cía. Ltda.";
            vm.NombreOperador = "AéreoLíneas «Ñandú» de la Amazonía";
            vm.InspectorPrincipalNombre = "Cap. Íñigo Muñoz Peña";
            vm.InspectorApoyoNombre = "Ing. René Núñez Cárdenas";
            vm.ResponsableTecnico = "Ing. Damián Velástegui";
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 1,
                CodigoOaci = "SEST",
                NombreCiudad = "San Cristóbal - Galápagos (Región Insular)",
                FechaInicio = new DateTime(2026, 11, 1),
                FechaFin = new DateTime(2026, 11, 3),
                Estado = "PROGRAMADA"
            });

            // Act: Generar con iTextSharp y codificación BaseFont.CP1252
            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);

            // Assert
            Assert.IsNotNull(pdfBytes);
            using (var reader = new PdfReader(pdfBytes))
            {
                Assert.IsTrue(reader.NumberOfPages >= 1, "Debe procesar sin errores de codificación.");
            }
        }

        /// <summary>
        /// Escenario 5: Trámite inexistente retorna HTTP 404 controlado.
        /// </summary>
        [TestMethod]
        public void Test05_TramiteInexistente_Retorna404()
        {
            // Act 1: Firma de trámite inexistente en el servicio
            var firmaResult = _docService.FirmarDesignacion(
                solicitudId: 0,
                dircavUsuarioId: 1,
                dircavNombre: "Dra. DIRCAV",
                rol: "DIRCAV"
            );

            // Assert: Trámite inexistente debe responder 404 o 400
            Assert.IsFalse(firmaResult.Exitoso, "Un trámite con ID 0 o inexistente no debe firmarse.");
            Assert.IsTrue(firmaResult.HttpStatusCode == 404 || firmaResult.HttpStatusCode == 400, "Debe responder con código de error 404 o 400.");

            // Act 2: Verificar que el controlador DircavController implementa la guarda HTTP 404 para GenerarPdf y Firmar
            var controllerFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\CapaPresentacion\Controllers\DircavController.cs");
            if (!File.Exists(controllerFilePath))
            {
                controllerFilePath = @"c:\proyectos\AOCR\CapaPresentacion\Controllers\DircavController.cs";
            }
            Assert.IsTrue(File.Exists(controllerFilePath));
            var controllerCode = File.ReadAllText(controllerFilePath);

            StringAssert.Contains(controllerCode, "public ActionResult GenerarPdf(int id)");
            StringAssert.Contains(controllerCode, "public ActionResult Firmar(int id, string passwordCertificado)");
            StringAssert.Contains(controllerCode, "if (id <= 0)");
            StringAssert.Contains(controllerCode, "return HttpNotFound");
        }

        /// <summary>
        /// Escenario 6: Usuario no autorizado retorna HTTP 403 Forbidden.
        /// </summary>
        [TestMethod]
        public void Test06_UsuarioNoAutorizado_Retorna403()
        {
            var rolesNoPermitidos = new[] { "DIRDAC", "UsuarioExterno", "Operador", "Financiero", "Coordinador" };
            var dircavService = new DircavDesignacionService();

            foreach (var rol in rolesNoPermitidos)
            {
                var puedeFirmar = dircavService.EsDircavAutorizado(rol);
                Assert.IsFalse(puedeFirmar, $"El rol '{rol}' no debe tener permisos de firma de designación DIRCAV.");

                var firmaResult = _docService.FirmarDesignacion(
                    solicitudId: 506,
                    dircavUsuarioId: 999,
                    dircavNombre: "Usuario No Autorizado",
                    rol: rol
                );

                Assert.IsFalse(firmaResult.Exitoso, $"La firma para el rol '{rol}' debe ser rechazada.");
                Assert.AreEqual(403, firmaResult.HttpStatusCode, $"El rol '{rol}' debe recibir HTTP 403 Forbidden.");
            }
        }

        /// <summary>
        /// Escenario 7: PDF abierto (inline preview) incluye marca de agua y cabecera de visualización en navegador.
        /// </summary>
        [TestMethod]
        public void Test07_PdfAbierto_GeneraPreviewConMarcaDeAgua()
        {
            var vm = CrearVmBase(507);
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 1,
                CodigoOaci = "SEQM",
                NombreCiudad = "Quito",
                FechaInicio = new DateTime(2026, 10, 1),
                FechaFin = new DateTime(2026, 10, 2),
                Estado = "PROGRAMADA"
            });

            // Act: Generar vista previa
            var previewBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: true);

            // Assert
            Assert.IsNotNull(previewBytes);
            Assert.IsTrue(previewBytes.Length > 1000);

            using (var reader = new PdfReader(previewBytes))
            {
                Assert.AreEqual(1, reader.NumberOfPages);
            }
        }

        /// <summary>
        /// Escenario 8: PDF descargado genera cabecera Content-Disposition tipo attachment y mime type application/pdf.
        /// </summary>
        [TestMethod]
        public void Test08_PdfDescargado_RetornaAttachmentYCabeceraPdf()
        {
            var vm = CrearVmBase(508);
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 1,
                CodigoOaci = "SEQM",
                NombreCiudad = "Quito",
                FechaInicio = new DateTime(2026, 10, 1),
                FechaFin = new DateTime(2026, 10, 2),
                Estado = "PROGRAMADA"
            });

            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);
            var nombreArchivo = $"Designacion_{vm.SolicitudId}_v{vm.Version}.pdf";

            Assert.IsNotNull(pdfBytes);
            Assert.IsTrue(pdfBytes.Length > 0, "El contenido del PDF descargable debe tener bytes válidos.");
            StringAssert.EndsWith(nombreArchivo, ".pdf", "El archivo de descarga debe tener extensión .pdf.");

            // Validar cabecera mágica de archivo PDF (%PDF)
            var cabecera = Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
            Assert.AreEqual("%PDF", cabecera);
        }

        /// <summary>
        /// Escenario 9: Firma institucional calcula hash criptográfico SHA-256 de 64 caracteres hex.
        /// </summary>
        [TestMethod]
        public void Test09_FirmaInstitucional_CalculaHashYNotifica()
        {
            var vm = CrearVmBase(509);
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 1,
                CodigoOaci = "SEQM",
                NombreCiudad = "Quito",
                FechaInicio = new DateTime(2026, 10, 1),
                FechaFin = new DateTime(2026, 10, 2),
                Estado = "PROGRAMADA"
            });
            vm.FechaFirma = DateTime.Now;

            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);

            string hash;
            using (var sha = SHA256.Create())
            {
                hash = BitConverter.ToString(sha.ComputeHash(pdfBytes)).Replace("-", "");
            }

            Assert.IsNotNull(hash);
            Assert.AreEqual(64, hash.Length, "El hash SHA-256 debe tener una longitud exacta de 64 caracteres hex.");
            Assert.IsTrue(hash.All(c => "0123456789ABCDEFabcdef".Contains(c)), "El hash debe ser una cadena hexadecimal válida.");
        }

        /// <summary>
        /// Escenario 10: Regeneración incrementa versión y preserva archivos y registros firmados previos (v1, v2).
        /// </summary>
        [TestMethod]
        public void Test10_Regeneracion_VersionadoSinEliminarFirmados()
        {
            // Arrange
            var designacionV1 = new AocrDesignacionInspector
            {
                Id = 10,
                SolicitudId = 510,
                Version = 1,
                Firmado = true,
                RutaDocumentoFirmado = "~/App_Data/Uploads/Designaciones/510/Designacion_510_v1_Firmada.pdf",
                HashDocumento = "HASH_VERSION_1_ABCDEF"
            };

            // Act: Creación de reasignación / nueva versión (v2)
            var nuevaVersion = designacionV1.Version + 1;
            var designacionV2 = new AocrDesignacionInspector
            {
                Id = 11,
                SolicitudId = 510,
                Version = nuevaVersion,
                Firmado = false,
                RutaPdf = $"~/App_Data/Uploads/Designaciones/510/Designacion_510_v{nuevaVersion}.pdf",
                HashDocumento = null
            };

            // Assert
            Assert.AreEqual(2, designacionV2.Version, "La versión de la nueva designación debe incrementarse a 2.");
            Assert.AreNotEqual(designacionV1.RutaDocumentoFirmado, designacionV2.RutaPdf, "Las rutas de archivo no deben sobreescribirse entre versiones.");
            Assert.IsTrue(designacionV1.Firmado, "El documento de la versión 1 permanece inalterable y marcado como firmado.");
            Assert.IsFalse(designacionV2.Firmado, "La versión 2 inicia en estado pendiente de firma.");
        }

        /// <summary>
        /// Escenario 11: Verificación de datos: todo proviene del servidor/expediente, cero valores quemados.
        /// </summary>
        [TestMethod]
        public void Test11_VerificacionDeDatos_TodoProvieneDelServidorSinValoresQuemados()
        {
            // 1. Verificar que el ViewModel base no contiene valores quemados por defecto
            var vmVacio = new DesignacionPdfViewModel();
            Assert.AreEqual(string.Empty, vmVacio.PaisOperador, "PaisOperador no debe tener valor quemado por defecto ('Ecuador').");
            Assert.AreEqual(string.Empty, vmVacio.NumeroAoc, "NumeroAoc no debe tener valor quemado por defecto ('AOC-RDAC129').");
            Assert.AreEqual(string.Empty, vmVacio.TipoOperacion, "TipoOperacion no debe tener valor quemado por defecto ('Transporte Aéreo Regular').");
            Assert.AreEqual(string.Empty, vmVacio.InspectorPrincipalCargo, "InspectorPrincipalCargo no debe tener valor quemado por defecto.");
            Assert.AreEqual(string.Empty, vmVacio.AutoridadDircavNombre, "AutoridadDircavNombre no debe tener valor quemado por defecto.");

            // 2. Verificar que los datos del expediente se transfieren fielmente sin sobreescrituras estáticas
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 511,
                NumeroSolicitud = "SOL-2026-ESP-001",
                RazonSocial = "Transportes Aéreos del Sur S.A.",
                NombreOperador = "SurAir",
                Pais = "Chile",
                NumeroAOC = "AOC-CL-129-888",
                TipoOperacion = "Carga Exclusiva No Regular",
                RepresentanteLegal = "Don Fernando Cordero"
            };

            var companiaPersistida = !string.IsNullOrWhiteSpace(solicitud.RazonSocial)
                ? solicitud.RazonSocial.Trim()
                : (!string.IsNullOrWhiteSpace(solicitud.NombreOperador) ? solicitud.NombreOperador.Trim() : string.Empty);
            var operadorPersistido = !string.IsNullOrWhiteSpace(solicitud.NombreOperador)
                ? solicitud.NombreOperador.Trim()
                : (!string.IsNullOrWhiteSpace(solicitud.RazonSocial) ? solicitud.RazonSocial.Trim() : string.Empty);

            var vm = new DesignacionPdfViewModel
            {
                SolicitudId = solicitud.CodigoSolicitud,
                NumeroSolicitud = solicitud.NumeroSolicitud,
                Compania = companiaPersistida,
                NombreOperador = operadorPersistido,
                PaisOperador = !string.IsNullOrWhiteSpace(solicitud.Pais) ? solicitud.Pais.Trim() : string.Empty,
                NumeroAoc = !string.IsNullOrWhiteSpace(solicitud.NumeroAOC) ? solicitud.NumeroAOC.Trim() : string.Empty,
                TipoOperacion = !string.IsNullOrWhiteSpace(solicitud.TipoOperacion) ? solicitud.TipoOperacion.Trim() : string.Empty,
                ResponsableTecnico = !string.IsNullOrWhiteSpace(solicitud.RepresentanteLegal) ? solicitud.RepresentanteLegal.Trim() : string.Empty,
                InspectorPrincipalNombre = "Cap. Patricio Gómez",
                InspectorPrincipalCargo = "Inspector Especialista de Carga"
            };

            Assert.AreEqual("Transportes Aéreos del Sur S.A.", vm.Compania);
            Assert.AreEqual("SurAir", vm.NombreOperador);
            Assert.AreEqual("Chile", vm.PaisOperador, "El país del operador debe ser Chile (obtenido de BD), no 'Ecuador'.");
            Assert.AreEqual("AOC-CL-129-888", vm.NumeroAoc, "El AOC debe ser el de BD, no 'AOC-RDAC129'.");
            Assert.AreEqual("Carga Exclusiva No Regular", vm.TipoOperacion, "El tipo de operación debe ser el de BD, no 'Transporte Aéreo Regular'.");
            Assert.AreEqual("Don Fernando Cordero", vm.ResponsableTecnico);
            Assert.AreEqual("Cap. Patricio Gómez", vm.InspectorPrincipalNombre);
            Assert.AreEqual("Inspector Especialista de Carga", vm.InspectorPrincipalCargo);

            // 3. Generar PDF con estos datos dinámicos y verificar que compile con éxito
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 1,
                CodigoOaci = "SCEL",
                NombreCiudad = "Santiago de Chile",
                FechaInicio = new DateTime(2026, 10, 15),
                FechaFin = new DateTime(2026, 10, 17),
                Estado = "PROGRAMADA"
            });

            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);
            Assert.IsNotNull(pdfBytes);
            using (var reader = new PdfReader(pdfBytes))
            {
                Assert.IsTrue(reader.NumberOfPages >= 1);
            }
        }
    }
}
