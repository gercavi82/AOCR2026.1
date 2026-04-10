using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;

namespace CapaNegocio.Services
{
    public class FirmaDigitalService
    {
        public InformacionCertificadoDigital LeerCertificado(byte[] certificadoBytes, string passwordCertificado)
        {
            try
            {
                if (certificadoBytes == null || certificadoBytes.Length == 0)
                {
                    return InformacionCertificadoDigital.Error("Debe cargar un certificado digital válido en formato .p12 o .pfx.");
                }

                if (string.IsNullOrWhiteSpace(passwordCertificado))
                {
                    return InformacionCertificadoDigital.Error("Debe ingresar la contraseña del certificado digital.");
                }

                AsymmetricKeyParameter llavePrivada;
                X509Certificate[] cadena;
                X509Certificate certificado;
                var carga = CargarCertificado(certificadoBytes, passwordCertificado, out llavePrivada, out cadena, out certificado);
                if (!string.IsNullOrWhiteSpace(carga))
                {
                    return InformacionCertificadoDigital.Error(carga);
                }

                if (certificado.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificado.NotAfter.ToUniversalTime() < DateTime.UtcNow)
                {
                    return InformacionCertificadoDigital.Error("El certificado digital no está vigente o se encuentra expirado.");
                }

                var sujeto = certificado.SubjectDN != null ? certificado.SubjectDN.ToString() : null;
                return InformacionCertificadoDigital.Ok(
                    sujeto,
                    ExtraerNombreComun(sujeto),
                    certificado.NotBefore.ToLocalTime(),
                    certificado.NotAfter.ToLocalTime());
            }
            catch (Exception ex)
            {
                return InformacionCertificadoDigital.Error("No se pudo leer el certificado digital: " + ex.Message);
            }
        }

        public ResultadoFirmaDigital FirmarPdf(byte[] pdfBytes, byte[] certificadoBytes, string passwordCertificado, string nombreFirmante, string motivo, string ubicacion, string rolFirmante, string contenidoQr = null)
        {
            try
            {
                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    return ResultadoFirmaDigital.Error("No existe un PDF generado para firmar.");
                }

                if (certificadoBytes == null || certificadoBytes.Length == 0)
                {
                    return ResultadoFirmaDigital.Error("Debe cargar un certificado digital válido en formato .p12 o .pfx.");
                }

                if (string.IsNullOrWhiteSpace(passwordCertificado))
                {
                    return ResultadoFirmaDigital.Error("Debe ingresar la contraseña del certificado digital.");
                }

                AsymmetricKeyParameter llavePrivada;
                X509Certificate[] cadena;
                X509Certificate certificado;
                var errorCarga = CargarCertificado(certificadoBytes, passwordCertificado, out llavePrivada, out cadena, out certificado);
                if (!string.IsNullOrWhiteSpace(errorCarga))
                {
                    return ResultadoFirmaDigital.Error(errorCarga);
                }

                if (certificado.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificado.NotAfter.ToUniversalTime() < DateTime.UtcNow)
                {
                    return ResultadoFirmaDigital.Error("El certificado digital no está vigente o se encuentra expirado.");
                }

                EnsureITextVersionInitialized();

                var pdfFuente = !string.IsNullOrWhiteSpace(contenidoQr)
                    ? EstamparQrEnPdf(pdfBytes, contenidoQr, rolFirmante)
                    : pdfBytes;

                using (var reader = new PdfReader(pdfFuente))
                using (var output = new MemoryStream())
                {
                    var tempFilePath = Path.GetTempFileName();
                    try
                    {
                        var stamp = PdfStamper.CreateSignature(reader, output, '\0', tempFilePath, true);
                        var appearance = stamp.SignatureAppearance;
                        appearance.Reason = string.IsNullOrWhiteSpace(motivo) ? "Firma digital AOCR" : motivo.Trim();
                        appearance.Location = string.IsNullOrWhiteSpace(ubicacion) ? "Sistema AOCR DGAC" : ubicacion.Trim();
                        appearance.SignDate = DateTime.Now;
                        appearance.Acro6Layers = true;
                        appearance.Layer2Text = string.Format(
                            "Firmado digitalmente por: {0}\nRol: {1}\nFecha: {2:dd/MM/yyyy HH:mm}",
                            string.IsNullOrWhiteSpace(nombreFirmante) ? "Usuario AOCR" : nombreFirmante.Trim(),
                            string.IsNullOrWhiteSpace(rolFirmante) ? "FIRMANTE" : rolFirmante.Trim().ToUpperInvariant(),
                            DateTime.Now);
                        appearance.SetVisibleSignature(ObtenerRectanguloFirma(rolFirmante), reader.NumberOfPages, ConstruirNombreFirma(rolFirmante));

                        var signature = new PrivateKeySignature(llavePrivada, DigestAlgorithms.SHA256);
                        MakeSignature.SignDetached(appearance, signature, cadena, null, null, null, 0, CryptoStandard.CMS);

                        var pdfFirmado = output.ToArray();
                        return ResultadoFirmaDigital.Ok(pdfFirmado, CalcularHash(pdfFirmado), certificado.SubjectDN != null ? certificado.SubjectDN.ToString() : null);
                    }
                    finally
                    {
                        try
                        {
                            if (File.Exists(tempFilePath))
                            {
                                File.Delete(tempFilePath);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return ResultadoFirmaDigital.Error("No se pudo aplicar la firma digital al PDF: " + ex.Message);
            }
        }

        private static Rectangle ObtenerRectanguloFirma(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "DIRDAC" || rol == "AOCR_FIRMANTE" || rol == "DIRECTOR_GENERAL")
            {
                return new Rectangle(315f, 36f, 495f, 118f);
            }

            return new Rectangle(36f, 36f, 280f, 118f);
        }

        private static Rectangle ObtenerRectanguloQr(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "DIRDAC" || rol == "AOCR_FIRMANTE" || rol == "DIRECTOR_GENERAL")
            {
                return new Rectangle(500f, 42f, 555f, 97f);
            }

            return new Rectangle(205f, 42f, 275f, 112f);
        }

        private static string ConstruirNombreFirma(string rolFirmante)
        {
            var rol = string.IsNullOrWhiteSpace(rolFirmante) ? "firmante" : rolFirmante.Trim().ToLowerInvariant();
            return string.Format("firma_{0}_{1:yyyyMMddHHmmss}", rol, DateTime.Now);
        }

        private static string CalcularHash(byte[] contenido)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(contenido ?? new byte[0]);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static void EnsureITextVersionInitialized()
        {
            var versionType = typeof(iTextSharp.text.Version);
            var versionField = versionType.GetField("version", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (versionField == null || versionField.GetValue(null) != null)
            {
                return;
            }

            var constructor = versionType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                return;
            }

            versionField.SetValue(null, constructor.Invoke(null));
        }

        private static byte[] EstamparQrEnPdf(byte[] pdfBytes, string contenidoQr, string rolFirmante)
        {
            using (var reader = new PdfReader(pdfBytes))
            using (var output = new MemoryStream())
            {
                using (var stamper = new PdfStamper(reader, output))
                {
                    var qr = new BarcodeQRCode(contenidoQr, 120, 120, null);
                    var qrImage = qr.GetImage();
                    var rect = ObtenerRectanguloQr(rolFirmante);
                    qrImage.ScaleAbsolute(rect.Right - rect.Left, rect.Top - rect.Bottom);
                    qrImage.SetAbsolutePosition(rect.Left, rect.Bottom);

                    var pagina = reader.NumberOfPages;
                    var canvas = stamper.GetOverContent(pagina);
                    canvas.AddImage(qrImage);
                }

                return output.ToArray();
            }
        }

        private static string CargarCertificado(byte[] certificadoBytes, string passwordCertificado, out AsymmetricKeyParameter llavePrivada, out X509Certificate[] cadena, out X509Certificate certificado)
        {
            llavePrivada = null;
            cadena = null;
            certificado = null;

            try
            {
                using (var certificadoStream = new MemoryStream(certificadoBytes))
                {
                    var store = new Pkcs12StoreBuilder().Build();
                    store.Load(certificadoStream, passwordCertificado.ToCharArray());
                    string alias = null;
                    foreach (string itemAlias in store.Aliases)
                    {
                        if (store.IsKeyEntry(itemAlias))
                        {
                            alias = itemAlias;
                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        return "El certificado digital no contiene una clave privada utilizable.";
                    }

                    llavePrivada = store.GetKey(alias).Key;
                    var chainEntries = store.GetCertificateChain(alias);
                    cadena = new X509Certificate[chainEntries.Length];
                    for (var index = 0; index < chainEntries.Length; index++)
                    {
                        cadena[index] = chainEntries[index].Certificate;
                    }

                    certificado = cadena[0];
                    return null;
                }
            }
            catch (Exception ex)
            {
                return "No se pudo leer el certificado digital: " + ex.Message;
            }
        }

        private static string ExtraerNombreComun(string sujetoCertificado)
        {
            if (string.IsNullOrWhiteSpace(sujetoCertificado))
            {
                return null;
            }

            var segmentos = sujetoCertificado.Split(',');
            foreach (var segmento in segmentos)
            {
                var parte = segmento.Trim();
                if (parte.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                {
                    return parte.Substring(3).Trim();
                }
            }

            return sujetoCertificado.Trim();
        }
    }

    public class InformacionCertificadoDigital
    {
        public bool Exitoso { get; private set; }
        public string Mensaje { get; private set; }
        public string SujetoCertificado { get; private set; }
        public string NombreTitular { get; private set; }
        public DateTime? VigenteDesde { get; private set; }
        public DateTime? VigenteHasta { get; private set; }

        private InformacionCertificadoDigital(bool exitoso, string mensaje, string sujetoCertificado, string nombreTitular, DateTime? vigenteDesde, DateTime? vigenteHasta)
        {
            Exitoso = exitoso;
            Mensaje = mensaje;
            SujetoCertificado = sujetoCertificado;
            NombreTitular = nombreTitular;
            VigenteDesde = vigenteDesde;
            VigenteHasta = vigenteHasta;
        }

        public static InformacionCertificadoDigital Ok(string sujetoCertificado, string nombreTitular, DateTime? vigenteDesde, DateTime? vigenteHasta)
        {
            return new InformacionCertificadoDigital(true, "OK", sujetoCertificado, nombreTitular, vigenteDesde, vigenteHasta);
        }

        public static InformacionCertificadoDigital Error(string mensaje)
        {
            return new InformacionCertificadoDigital(false, mensaje, null, null, null, null);
        }
    }

    public class ResultadoFirmaDigital
    {
        public bool Exitoso { get; private set; }
        public string Mensaje { get; private set; }
        public byte[] PdfFirmado { get; private set; }
        public string HashSha256 { get; private set; }
        public string SujetoCertificado { get; private set; }

        private ResultadoFirmaDigital(bool exitoso, string mensaje, byte[] pdfFirmado, string hashSha256, string sujetoCertificado)
        {
            Exitoso = exitoso;
            Mensaje = mensaje;
            PdfFirmado = pdfFirmado;
            HashSha256 = hashSha256;
            SujetoCertificado = sujetoCertificado;
        }

        public static ResultadoFirmaDigital Ok(byte[] pdfFirmado, string hashSha256, string sujetoCertificado)
        {
            return new ResultadoFirmaDigital(true, "OK", pdfFirmado, hashSha256, sujetoCertificado);
        }

        public static ResultadoFirmaDigital Error(string mensaje)
        {
            return new ResultadoFirmaDigital(false, mensaje, null, null, null);
        }
    }
}