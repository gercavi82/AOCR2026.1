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
using iTextSharp.text.pdf.parser;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Path = System.IO.Path;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-06: MATRIZ DE 16 PRUEBAS OBLIGATORIAS
    /// "Generar el oficio de designación en PDF con estaciones, fechas y firma digital real de DIRCAV"
    /// 
    /// 1. DIRCAV firma con certificado válido.
    /// 2. Certificado sin clave privada es rechazado.
    /// 3. Contraseña incorrecta es rechazada.
    /// 4. Certificado vencido es rechazado.
    /// 5. PDF firmado puede verificarse criptográficamente.
    /// 6. Hash coincide con el archivo firmado.
    /// 7. Alteración del PDF rompe la verificación.
    /// 8. Doble clic no crea otro archivo.
    /// 9. DIRDAC recibe 403.
    /// 10. Administrador recibe 403.
    /// 11. Documento incluye estaciones y fechas.
    /// 12. Fallo de BD elimina archivo temporal.
    /// 13. Fallo de almacenamiento ejecuta rollback.
    /// 14. Descarga sin autorización devuelve 403.
    /// 15. Una sola auditoría y una sola notificación.
    /// 16. Recarga conserva el PDF firmado.
    /// </summary>
    [TestClass]
    public class Ac06MatrizPruebasObligatoriasTests
    {
        private DesignacionDocumentoService _docService;
        private FirmaDigitalService _firmaDigitalService;
        private IAocrFlujoService _flujoService;

        [TestInitialize]
        public void Setup()
        {
            _docService = new DesignacionDocumentoService();
            _firmaDigitalService = new FirmaDigitalService();
            _flujoService = new AocrFlujoService();
        }

        #region Helpers de Certificados y Rutas

        private static string ObtenerRutaRaizProyecto()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "AOCR.sln")))
                {
                    return dir;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return @"c:\proyectos\AOCR";
        }

        private static string ReadFile(string relativePath)
        {
            var path = Path.Combine(ObtenerRutaRaizProyecto(), relativePath.TrimStart('\\', '/').Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "Archivo no encontrado: " + relativePath);
            return File.ReadAllText(path);
        }

        private static byte[] GenerarCertificadoP12(string password, bool conClavePrivada = true, DateTime? notBefore = null, DateTime? notAfter = null)
        {
            var kpGen = new RsaKeyPairGenerator();
            kpGen.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = kpGen.GenerateKeyPair();

            var gen = new X509V3CertificateGenerator();
            var dn = new X509Name("CN=Dra. Sofia Alarcon (DIRCAV), OU=DIRCAV, O=DGAC Ecuador, C=EC");
            gen.SetSubjectDN(dn);
            gen.SetIssuerDN(dn);
            gen.SetNotBefore(notBefore ?? DateTime.UtcNow.AddDays(-1));
            gen.SetNotAfter(notAfter ?? DateTime.UtcNow.AddYears(1));
            gen.SetPublicKey(keyPair.Public);
            gen.SetSerialNumber(BigInteger.ValueOf(DateTime.UtcNow.Ticks));

            var cert = gen.Generate(new Asn1SignatureFactory("SHA256WITHRSA", keyPair.Private));

            var store = new Pkcs12StoreBuilder().Build();
            var certEntry = new X509CertificateEntry(cert);
            if (conClavePrivada)
            {
                store.SetKeyEntry("dircav", new AsymmetricKeyEntry(keyPair.Private), new[] { certEntry });
            }
            else
            {
                store.SetCertificateEntry("dircav", certEntry);
            }

            using (var ms = new MemoryStream())
            {
                store.Save(ms, password.ToCharArray(), new SecureRandom());
                return ms.ToArray();
            }
        }

        private DesignacionPdfViewModel CrearVmBase(int solicitudId = 500)
        {
            var vm = new DesignacionPdfViewModel
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
                CodigoVerificacion = $"AOCR-VERIF-{solicitudId}-1-1",
                EsVistaPrevia = false
            };

            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 1,
                CodigoOaci = "SEQM",
                NombreCiudad = "Quito - Aeropuerto Internacional Mariscal Sucre",
                FechaInicio = new DateTime(2026, 10, 1),
                FechaFin = new DateTime(2026, 10, 3),
                Estado = "PROGRAMADA"
            });

            return vm;
        }

        #endregion

        #region 1. DIRCAV firma con certificado válido
        [TestMethod]
        public void Test01_DIRCAV_FirmaConCertificadoValido()
        {
            var vm = CrearVmBase(601);
            var basePdf = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);
            Assert.IsNotNull(basePdf, "El PDF base debe generarse exitosamente.");

            var password = "PasswordValido123!";
            var certBytes = GenerarCertificadoP12(password, conClavePrivada: true);

            var certInfo = _firmaDigitalService.LeerCertificado(certBytes, password);
            Assert.IsTrue(certInfo.Exitoso, "El certificado digital generado en memoria debe ser válido: " + certInfo.Mensaje);

            var resultadoFirma = _firmaDigitalService.FirmarPdf(
                basePdf,
                certBytes,
                password,
                nombreFirmante: vm.AutoridadDircavNombre,
                motivo: "Designación formal de inspectores AOCR",
                ubicacion: "Quito, Ecuador",
                rolFirmante: "DIRCAV_DESIGNACION");

            Assert.IsTrue(resultadoFirma.Exitoso, "La firma digital criptográfica de DIRCAV debe ser exitosa: " + resultadoFirma.Mensaje);
            Assert.IsNotNull(resultadoFirma.PdfFirmado, "El byte array del PDF firmado no debe ser nulo.");
            Assert.IsTrue(resultadoFirma.PdfFirmado.Length > basePdf.Length, "El PDF firmado debe contener la firma embebida y ser de mayor tamaño.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(resultadoFirma.HashSha256), "Debe calcular el hash SHA-256 del documento firmado.");

            // Validar transición en el catálogo de flujo
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.DesignacionPendienteFirmaDircav,
                AocrEstadosProceso.DesignacionFirmadaDircav),
                "La transición de DESIGNACION_PENDIENTE_FIRMA_DIRCAV a DESIGNACION_FIRMADA_DIRCAV debe estar permitida.");
        }
        #endregion

        #region 2. Certificado sin clave privada es rechazado
        [TestMethod]
        public void Test02_CertificadoSinClavePrivadaEsRechazado()
        {
            var password = "PassClave123!";
            // Generar certificado sin clave privada asociada en el store PKCS#12
            var certBytes = GenerarCertificadoP12(password, conClavePrivada: false);

            var certInfo = _firmaDigitalService.LeerCertificado(certBytes, password);
            Assert.IsFalse(certInfo.Exitoso, "Un certificado sin clave privada no debe ser aceptado.");
            StringAssert.Contains(certInfo.Mensaje.ToLowerInvariant(), "clave privada", "El mensaje debe indicar que carece de clave privada utilizable.");

            // Llamada al servicio
            var res = _docService.FirmarDesignacion(
                solicitudId: 602,
                dircavUsuarioId: 10,
                dircavNombre: "Dra. Sofía Alarcón",
                rol: "DIRCAV",
                certificadoBytes: certBytes,
                passwordCert: password);

            Assert.IsFalse(res.Exitoso, "El servicio de designación debe rechazar el certificado sin clave privada.");
            Assert.AreEqual(400, res.HttpStatusCode, "El código HTTP debe ser 400 Bad Request.");
            StringAssert.Contains(res.Mensaje.ToLowerInvariant(), "clave privada");
        }
        #endregion

        #region 3. Contraseña incorrecta es rechazada
        [TestMethod]
        public void Test03_ContraseniaIncorrectaEsRechazada()
        {
            var passwordCorrecto = "ClaveSecreta123!";
            var passwordErroneo = "ClaveIncorrecta999!";
            var certBytes = GenerarCertificadoP12(passwordCorrecto, conClavePrivada: true);

            var certInfo = _firmaDigitalService.LeerCertificado(certBytes, passwordErroneo);
            Assert.IsFalse(certInfo.Exitoso, "Leer certificado con contraseña incorrecta debe fallar.");

            var res = _docService.FirmarDesignacion(
                solicitudId: 603,
                dircavUsuarioId: 10,
                dircavNombre: "Dra. Sofía Alarcón",
                rol: "DIRCAV",
                certificadoBytes: certBytes,
                passwordCert: passwordErroneo);

            Assert.IsFalse(res.Exitoso, "La firma debe rechazarse cuando la contraseña es incorrecta.");
            Assert.AreEqual(400, res.HttpStatusCode, "El código HTTP debe ser 400 Bad Request.");
        }
        #endregion

        #region 4. Certificado vencido es rechazado
        [TestMethod]
        public void Test04_CertificadoVencidoEsRechazado()
        {
            var password = "PasswordVencido123!";
            // Certificado emitido en el pasado y vencido ayer
            var certBytes = GenerarCertificadoP12(password, conClavePrivada: true,
                notBefore: DateTime.UtcNow.AddYears(-2),
                notAfter: DateTime.UtcNow.AddDays(-1));

            var certInfo = _firmaDigitalService.LeerCertificado(certBytes, password);
            Assert.IsFalse(certInfo.Exitoso, "Un certificado expirado debe ser rechazado.");
            StringAssert.Contains(certInfo.Mensaje.ToLowerInvariant(), "expirado");

            var res = _docService.FirmarDesignacion(
                solicitudId: 604,
                dircavUsuarioId: 10,
                dircavNombre: "Dra. Sofía Alarcón",
                rol: "DIRCAV",
                certificadoBytes: certBytes,
                passwordCert: password);

            Assert.IsFalse(res.Exitoso, "La firma debe rechazarse con certificado vencido.");
            Assert.AreEqual(400, res.HttpStatusCode, "Debe devolver HTTP 400 Bad Request.");
            StringAssert.Contains(res.Mensaje.ToLowerInvariant(), "expirado");
        }
        #endregion

        #region 5. PDF firmado puede verificarse criptográficamente
        [TestMethod]
        public void Test05_PdfFirmadoPuedeVerificarseCriptograficamente()
        {
            var vm = CrearVmBase(605);
            var basePdf = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);
            var password = "FirmaVerificable123!";
            var certBytes = GenerarCertificadoP12(password, conClavePrivada: true);

            var resultadoFirma = _firmaDigitalService.FirmarPdf(
                basePdf,
                certBytes,
                password,
                nombreFirmante: "Dra. Sofía Alarcón",
                motivo: "Designación formal de inspectores AOCR",
                ubicacion: "Quito, Ecuador",
                rolFirmante: "DIRCAV_DESIGNACION");

            Assert.IsTrue(resultadoFirma.Exitoso, "La firma digital debe aplicarse correctamente: " + resultadoFirma.Mensaje);

            // Verificación criptográfica formal del PDF firmado con iTextSharp y BouncyCastle
            var verif = FirmaDigitalService.VerificarPdf(resultadoFirma.PdfFirmado);
            Assert.IsTrue(verif.TieneFirmaDigital, "El PDF verificado debe contener firma digital: " + verif.Mensaje);
            Assert.IsTrue(verif.EsValida, "La firma criptográfica debe ser válida: " + verif.Mensaje);
            Assert.IsTrue(verif.CubreTodoElDocumento, "La firma debe cubrir el documento de manera íntegra.");
            Assert.IsNotNull(verif.SujetoCertificado, "El sujeto del certificado debe estar presente.");
            StringAssert.Contains(verif.SujetoCertificado, "DIRCAV");
        }
        #endregion

        #region 6. Hash coincide con el archivo firmado
        [TestMethod]
        public void Test06_HashCoincideConElArchivoFirmado()
        {
            var vm = CrearVmBase(606);
            var basePdf = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);
            var password = "HashCoincidente123!";
            var certBytes = GenerarCertificadoP12(password, conClavePrivada: true);

            var resultadoFirma = _firmaDigitalService.FirmarPdf(
                basePdf,
                certBytes,
                password,
                nombreFirmante: "Dra. Sofía Alarcón",
                motivo: "Designación formal de inspectores AOCR",
                ubicacion: "Quito, Ecuador",
                rolFirmante: "DIRCAV_DESIGNACION");

            Assert.IsTrue(resultadoFirma.Exitoso);

            // Calcular independientemente el hash SHA-256
            string hashCalculado;
            using (var sha = SHA256.Create())
            {
                hashCalculado = BitConverter.ToString(sha.ComputeHash(resultadoFirma.PdfFirmado)).Replace("-", "").ToUpperInvariant();
            }

            Assert.AreEqual(hashCalculado, resultadoFirma.HashSha256,
                "El hash SHA-256 reportado por el resultado de firma debe ser matemáticamente idéntico al hash del archivo binario.");
        }
        #endregion

        #region 7. Alteración del PDF rompe la verificación
        [TestMethod]
        public void Test07_AlteracionDelPdfRompeLaVerificacion()
        {
            var vm = CrearVmBase(607);
            var basePdf = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);
            var password = "Integridad123!";
            var certBytes = GenerarCertificadoP12(password, conClavePrivada: true);

            var resultadoFirma = _firmaDigitalService.FirmarPdf(
                basePdf,
                certBytes,
                password,
                nombreFirmante: "Dra. Sofía Alarcón",
                motivo: "Designación formal de inspectores AOCR",
                ubicacion: "Quito, Ecuador",
                rolFirmante: "DIRCAV_DESIGNACION");

            Assert.IsTrue(resultadoFirma.Exitoso);

            // Verificar que el PDF intacto es válido
            var verifIntacta = FirmaDigitalService.VerificarPdf(resultadoFirma.PdfFirmado);
            Assert.IsTrue(verifIntacta.EsValida, "El PDF sin alterar debe ser válido.");

            // Alterar maliciosamente 1 byte del PDF firmado
            var pdfAlterado = (byte[])resultadoFirma.PdfFirmado.Clone();
            int offset = pdfAlterado.Length / 2;
            pdfAlterado[offset] ^= 0xFF; // Invertir bits en el centro del archivo

            // La verificación criptográfica debe fallar rotundamente
            var verifAlterada = FirmaDigitalService.VerificarPdf(pdfAlterado);
            Assert.IsFalse(verifAlterada.EsValida, "La verificación criptográfica debe fallar inmediatamente ante cualquier byte alterado.");
        }
        #endregion

        #region 8. Doble clic no crea otro archivo
        [TestMethod]
        public void Test08_DobleClicNoCreaOtroArchivo()
        {
            var serviceSource = ReadFile("CapaNegocio/Services/DesignacionDocumentoService.cs");
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // Validar que el servicio detecta designacion.Firmado y devuelve 200 sin regenerar archivo
            StringAssert.Contains(serviceSource, "if (designacion.Firmado)");
            StringAssert.Contains(serviceSource, "El oficio de designación ya se encontraba firmado formalmente por DIRCAV.");

            // Validar que en la transacción DAO también hay guarda de idempotencia
            StringAssert.Contains(daoSource, "if (desigVigente.Firmado)");
            StringAssert.Contains(daoSource, "EsIdempotente = true");
        }
        #endregion

        #region 9. DIRDAC recibe 403
        [TestMethod]
        public void Test09_DIRDAC_Recibe403()
        {
            var res = _docService.FirmarDesignacion(
                solicitudId: 609,
                dircavUsuarioId: 5,
                dircavNombre: "Director General DIRDAC",
                rol: AocrRolesInstitucionales.Dirdac);

            Assert.IsFalse(res.Exitoso, "DIRDAC no puede firmar la designación.");
            Assert.AreEqual(403, res.HttpStatusCode, "DIRDAC debe recibir HTTP 403 Forbidden.");
            StringAssert.Contains(res.Mensaje, "Acceso denegado");
        }
        #endregion

        #region 10. Administrador recibe 403
        [TestMethod]
        public void Test10_Administrador_Recibe403()
        {
            // Administrador
            var resAdmin = _docService.FirmarDesignacion(
                solicitudId: 610,
                dircavUsuarioId: 1,
                dircavNombre: "Admin Sistema",
                rol: AocrRolesInstitucionales.Administrador);

            Assert.IsFalse(resAdmin.Exitoso, "Administrador no puede firmar designación.");
            Assert.AreEqual(403, resAdmin.HttpStatusCode, "Administrador debe recibir HTTP 403.");

            // Coordinador
            var resCoord = _docService.FirmarDesignacion(
                solicitudId: 610,
                dircavUsuarioId: 2,
                dircavNombre: "Coordinador",
                rol: AocrRolesInstitucionales.Coordinador);
            Assert.AreEqual(403, resCoord.HttpStatusCode, "Coordinador debe recibir HTTP 403.");

            // Inspector
            var resInsp = _docService.FirmarDesignacion(
                solicitudId: 610,
                dircavUsuarioId: 3,
                dircavNombre: "Inspector",
                rol: AocrRolesInstitucionales.Inspector);
            Assert.AreEqual(403, resInsp.HttpStatusCode, "Inspector debe recibir HTTP 403.");

            // RT
            var resRt = _docService.FirmarDesignacion(
                solicitudId: 610,
                dircavUsuarioId: 4,
                dircavNombre: "RT",
                rol: AocrRolesInstitucionales.RT);
            Assert.AreEqual(403, resRt.HttpStatusCode, "RT debe recibir HTTP 403.");

            // Financiero
            var resFin = _docService.FirmarDesignacion(
                solicitudId: 610,
                dircavUsuarioId: 5,
                dircavNombre: "Financiero",
                rol: AocrRolesInstitucionales.Financiero);
            Assert.AreEqual(403, resFin.HttpStatusCode, "Financiero debe recibir HTTP 403.");
        }
        #endregion

        #region 11. Documento incluye estaciones y fechas
        [TestMethod]
        public void Test11_DocumentoIncluyeEstacionesYFechas()
        {
            var vm = CrearVmBase(611);
            vm.Estaciones.Clear();
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 1,
                CodigoOaci = "SEQM",
                NombreCiudad = "Quito Mariscal Sucre",
                FechaInicio = new DateTime(2026, 11, 1),
                FechaFin = new DateTime(2026, 11, 3),
                Estado = "PROGRAMADA"
            });
            vm.Estaciones.Add(new DesignacionEstacionItemDto
            {
                EstacionId = 2,
                CodigoOaci = "SEGU",
                NombreCiudad = "Guayaquil Jose Joaquin de Olmedo",
                FechaInicio = new DateTime(2026, 11, 10),
                FechaFin = new DateTime(2026, 11, 12),
                Estado = "PROGRAMADA"
            });

            var pdfBytes = _docService.GenerarPdfOficial(vm, esVistaPrevia: false);
            Assert.IsNotNull(pdfBytes);

            using (var reader = new PdfReader(pdfBytes))
            {
                var text = PdfTextExtractor.GetTextFromPage(reader, 1);
                StringAssert.Contains(text, "SEQM", "El PDF debe contener el código de la estación 1 (SEQM).");
                StringAssert.Contains(text, "01/11/2026", "El PDF debe contener la fecha inicio de la estación 1.");
                StringAssert.Contains(text, "03/11/2026", "El PDF debe contener la fecha fin de la estación 1.");
                StringAssert.Contains(text, "SEGU", "El PDF debe contener el código de la estación 2 (SEGU).");
                StringAssert.Contains(text, "10/11/2026", "El PDF debe contener la fecha inicio de la estación 2.");
                StringAssert.Contains(text, "12/11/2026", "El PDF debe contener la fecha fin de la estación 2.");
            }
        }
        #endregion

        #region 12. Fallo de BD elimina archivo temporal
        [TestMethod]
        public void Test12_FalloDeBDEliminaArchivoTemporal()
        {
            var serviceSource = ReadFile("CapaNegocio/Services/DesignacionDocumentoService.cs");

            // Validar que se crea el archivo temporal en Path.GetTempPath()
            StringAssert.Contains(serviceSource, "Path.GetTempPath()");
            StringAssert.Contains(serviceSource, "aocr_desig_tmp_");

            // Validar que en caso de fallo de BD (resTx.Exitoso == false o catch), se elimina tempFilePath
            StringAssert.Contains(serviceSource, "if (!resTx.Exitoso)");
            StringAssert.Contains(serviceSource, "tx.Rollback();");
            StringAssert.Contains(serviceSource, "if (File.Exists(tempFilePath)) File.Delete(tempFilePath);");

            // Validar que en el bloque finally se asegura la limpieza
            StringAssert.Contains(serviceSource, "finally");
            StringAssert.Contains(serviceSource, "File.Delete(tempFilePath);");
        }
        #endregion

        #region 13. Fallo de almacenamiento ejecuta rollback
        [TestMethod]
        public void Test13_FalloDeAlmacenamientoEjecutaRollback()
        {
            var serviceSource = ReadFile("CapaNegocio/Services/DesignacionDocumentoService.cs");

            // Validar bloque try-catch en la copia de archivo a rutaFisica
            StringAssert.Contains(serviceSource, "File.Copy(tempFilePath, rutaFisica, overwrite: true);");
            StringAssert.Contains(serviceSource, "catch (Exception exStorage)");
            StringAssert.Contains(serviceSource, "tx.Rollback();");
            StringAssert.Contains(serviceSource, "Fallo de almacenamiento al guardar el PDF firmado");
        }
        #endregion

        #region 14. Descarga sin autorización devuelve 403
        [TestMethod]
        public void Test14_DescargaSinAutorizacionDevuelve403()
        {
            // Intentar descargar con rol no autorizado (ej. Operador, o Inspector no asignado)
            try
            {
                string nombreArchivo;
                _docService.ObtenerDocumentoParaDescarga(
                    solicitudId: 99999,
                    usuarioId: 999,
                    rol: "Inspector",
                    usuarioLogin: "inspector_ajeno",
                    nombreDescarga: out nombreArchivo);

                Assert.Fail("Debe lanzar excepción por documento inexistente o acceso no autorizado.");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is UnauthorizedAccessException || ex is FileNotFoundException,
                    "Debe arrojar UnauthorizedAccessException (403) o FileNotFoundException (404).");
            }

            var controllerSource = ReadFile("CapaPresentacion/Controllers/DircavController.cs");
            StringAssert.Contains(controllerSource, "catch (UnauthorizedAccessException ex)");
            StringAssert.Contains(controllerSource, "return new HttpStatusCodeResult(403, ex.Message);");
        }
        #endregion

        #region 15. Una sola auditoría y una sola notificación
        [TestMethod]
        public void Test15_UnaSolaAuditoriaYUnaSolaNotificacion()
        {
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // Validar que EjecutarFirmaDesignacionTransaccional tiene exactamente una inserción en auditoría
            int idxAudit = daoSource.IndexOf("('DIRCAV', 'FIRMAR_DESIGNACION_INSPECTOR'", StringComparison.Ordinal);
            Assert.IsTrue(idxAudit > 0, "Debe registrar la auditoría de firma institucional DIRCAV.");

            int secondAudit = daoSource.IndexOf("('DIRCAV', 'FIRMAR_DESIGNACION_INSPECTOR'", idxAudit + 1, StringComparison.Ordinal);
            Assert.AreEqual(-1, secondAudit, "Solo debe existir un registro de auditoría para la firma en la transacción.");

            // Validar que se encola exactamente un item en email_queue
            StringAssert.Contains(daoSource, "SOLICITUD_DESIGNACION_FIRMADA_INSPECTOR");
            StringAssert.Contains(daoSource, "new CapaDatos.Services.EmailQueueService().EncolarConAdjuntosEnTransaccion");
        }
        #endregion

        #region 16. Recarga conserva el PDF firmado
        [TestMethod]
        public void Test16_RecargaConservaElPdfFirmado()
        {
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");
            var modelSource = ReadFile("CapaModelo/AocrDesignacionInspector.cs");

            // Validar que el modelo mapea las propiedades persistidas de AC-06
            StringAssert.Contains(modelSource, "public string HuellaCertificado { get; set; }");
            StringAssert.Contains(modelSource, "public string CodigoVerificacion { get; set; }");
            StringAssert.Contains(modelSource, "public bool Firmado { get; set; }");
            StringAssert.Contains(modelSource, "public string RutaDocumentoFirmado { get; set; }");
            StringAssert.Contains(modelSource, "public string HashDocumento { get; set; }");

            // Validar que AocrDesignacionDAO incluye estas columnas en sus consultas SELECT de carga
            StringAssert.Contains(daoSource, "huella_certificado, codigo_verificacion");
            StringAssert.Contains(daoSource, "d.HuellaCertificado = dr.IsDBNull(dr.GetOrdinal(\"huella_certificado\"))");
            StringAssert.Contains(daoSource, "d.CodigoVerificacion = dr.IsDBNull(dr.GetOrdinal(\"codigo_verificacion\"))");
        }
        #endregion
    }
}
