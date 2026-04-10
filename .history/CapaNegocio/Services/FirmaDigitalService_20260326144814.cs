using System;
using System.IO;
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
        public ResultadoFirmaDigital FirmarPdf(byte[] pdfBytes, byte[] certificadoBytes, string passwordCertificado, string nombreFirmante, string motivo, string ubicacion, string rolFirmante)
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
                        return ResultadoFirmaDigital.Error("El certificado digital no contiene una clave privada utilizable.");
                    }

                    llavePrivada = store.GetKey(alias).Key;
                    var chainEntries = store.GetCertificateChain(alias);
                    cadena = new X509Certificate[chainEntries.Length];
                    for (var index = 0; index < chainEntries.Length; index++)
                    {
                        cadena[index] = chainEntries[index].Certificate;
                    }

                    certificado = cadena[0];
                }

                if (certificado.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificado.NotAfter.ToUniversalTime() < DateTime.UtcNow)
                {
                    return ResultadoFirmaDigital.Error("El certificado digital no está vigente o se encuentra expirado.");
                }

                using (var reader = new PdfReader(pdfBytes))
                using (var output = new MemoryStream())
                {
                    var stamp = PdfStamper.CreateSignature(reader, output, '\0', null, true);
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
            }
            catch (Exception ex)
            {
                return ResultadoFirmaDigital.Error("No se pudo aplicar la firma digital al PDF: " + ex.Message);
            }
        }

        private static Rectangle ObtenerRectanguloFirma(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "DIRDAC")
            {
                return new Rectangle(315f, 36f, 560f, 118f);
            }

            return new Rectangle(36f, 36f, 280f, 118f);
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