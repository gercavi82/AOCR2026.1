using System;
using System.IO;
using System.Linq;
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

                var fechaFirma = DateTime.Now;
                var qrPayload = !string.IsNullOrWhiteSpace(contenidoQr)
                    ? contenidoQr
                    : ConstruirContenidoQrPorDefecto(nombreFirmante, rolFirmante, motivo, ubicacion, certificado, fechaFirma);

                var pdfFuente = EstamparBloqueFirmaVisual(pdfBytes, qrPayload, nombreFirmante, rolFirmante, fechaFirma);

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
                        appearance.SignDate = fechaFirma;
                        appearance.Acro6Layers = true;
                        appearance.Layer2Text = " ";

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
            if (rol == "AOCR_FIRMANTE")
            {
                return new Rectangle(372f, 668f, 540f, 756f);
            }

            if (rol == "INFORME_TECNICO_INSPECTOR")
            {
                return new Rectangle(390f, 576f, 538f, 666f);
            }

            if (rol == "INFORME_TECNICO_DIRDAC")
            {
                return new Rectangle(390f, 576f, 538f, 666f);
            }

            if (rol == "DIRDAC" || rol == "DIRECTOR_GENERAL")
            {
                return new Rectangle(300f, 30f, 565f, 135f);
            }

            return new Rectangle(30f, 30f, 295f, 135f);
        }

        private static Rectangle ObtenerRectanguloQr(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "AOCR_FIRMANTE")
            {
                var totalAocr = ObtenerRectanguloFirma(rolFirmante);
                return new Rectangle(totalAocr.Left + 5f, totalAocr.Bottom + 16f, totalAocr.Left + 53f, totalAocr.Bottom + 64f);
            }

            if (rol == "INFORME_TECNICO_INSPECTOR" || rol == "INFORME_TECNICO_DIRDAC")
            {
                var totalInforme = ObtenerRectanguloFirma(rolFirmante);
                return new Rectangle(totalInforme.Left + 6f, totalInforme.Bottom + 20f, totalInforme.Left + 46f, totalInforme.Bottom + 60f);
            }

            var total = ObtenerRectanguloFirma(rolFirmante);
            return new Rectangle(total.Left + 6f, total.Bottom + 6f, total.Left + 96f, total.Bottom + 96f);
        }

        private static Rectangle ObtenerRectanguloTextoFirma(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "AOCR_FIRMANTE")
            {
                var totalAocr = ObtenerRectanguloFirma(rolFirmante);
                return new Rectangle(totalAocr.Left + 58f, totalAocr.Bottom + 10f, totalAocr.Right - 6f, totalAocr.Top - 8f);
            }

            if (rol == "INFORME_TECNICO_INSPECTOR" || rol == "INFORME_TECNICO_DIRDAC")
            {
                var totalInforme = ObtenerRectanguloFirma(rolFirmante);
                return new Rectangle(totalInforme.Left + 50f, totalInforme.Bottom + 8f, totalInforme.Right - 4f, totalInforme.Top - 4f);
            }

            var total = ObtenerRectanguloFirma(rolFirmante);
            return new Rectangle(total.Left + 104f, total.Bottom + 8f, total.Right - 8f, total.Top - 8f);
        }

        private static bool EsFirmaIntegradaEnPlantilla(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            return rol == "AOCR_FIRMANTE"
                || rol == "INFORME_TECNICO_INSPECTOR"
                || rol == "INFORME_TECNICO_DIRDAC";
        }

        private static string ObtenerTituloBloqueFirma(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "INFORME_TECNICO_INSPECTOR")
            {
                return "Firmado electronicamente por:\n";
            }

            if (rol == "INFORME_TECNICO_DIRDAC" || rol == "AOCR_FIRMANTE")
            {
                if (rol == "INFORME_TECNICO_DIRDAC")
                {
                    return "Firmado electronicamente por:\n";
                }

                return "FIRMA AUTORIZACION\n";
            }

            return "Firmado electronicamente por:\n";
        }

        private static string ObtenerRolQr(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "INFORME_TECNICO_INSPECTOR")
            {
                return "INSPECTOR";
            }

            if (rol == "INFORME_TECNICO_DIRDAC")
            {
                return "DIRDAC";
            }

            return rolFirmante ?? string.Empty;
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

        private static byte[] EstamparBloqueFirmaVisual(byte[] pdfBytes, string contenidoQr, string nombreFirmante, string rolFirmante, DateTime fechaFirma)
        {
            using (var reader = new PdfReader(pdfBytes))
            using (var output = new MemoryStream())
            {
                using (var stamper = new PdfStamper(reader, output))
                {
                    var qr = new BarcodeQRCode(contenidoQr, 120, 120, null);
                    var qrImage = qr.GetImage();
                    var rectQr = ObtenerRectanguloQr(rolFirmante);
                    var rectTexto = ObtenerRectanguloTextoFirma(rolFirmante);
                    var rectTotal = ObtenerRectanguloFirma(rolFirmante);
                    qrImage.ScaleAbsolute(rectQr.Right - rectQr.Left, rectQr.Top - rectQr.Bottom);
                    qrImage.SetAbsolutePosition(rectQr.Left, rectQr.Bottom);

                    var pagina = reader.NumberOfPages;
                    var canvas = stamper.GetOverContent(pagina);
                    canvas.SaveState();
                    var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
                    var esAocr = rol == "AOCR_FIRMANTE";
                    var esFirmaIntegrada = EsFirmaIntegradaEnPlantilla(rolFirmante);
                    var esInformeTecnico = rol == "INFORME_TECNICO_INSPECTOR" || rol == "INFORME_TECNICO_DIRDAC";
                    if (esInformeTecnico)
                    {
                        canvas.SetColorFill(BaseColor.WHITE);
                        canvas.Rectangle(rectTotal.Left, rectTotal.Bottom, rectTotal.Right - rectTotal.Left, rectTotal.Top - rectTotal.Bottom);
                        canvas.Fill();
                        canvas.Rectangle(rectTotal.Left, rectTotal.Bottom, rectTotal.Right - rectTotal.Left, rectTotal.Top - rectTotal.Bottom);
                        canvas.Clip();
                        canvas.NewPath();
                    }
                    else if (esFirmaIntegrada)
                    {
                        canvas.Rectangle(rectTotal.Left, rectTotal.Bottom, rectTotal.Right - rectTotal.Left, rectTotal.Top - rectTotal.Bottom);
                        canvas.Clip();
                        canvas.NewPath();
                    }
                    else
                    {
                        canvas.SetColorStroke(BaseColor.BLACK);
                        canvas.SetLineWidth(0.8f);
                        canvas.Rectangle(rectTotal.Left, rectTotal.Bottom, rectTotal.Right - rectTotal.Left, rectTotal.Top - rectTotal.Bottom);
                        canvas.Stroke();
                    }
                    canvas.AddImage(qrImage);

                    var baseNormal = BaseFont.CreateFont(BaseFont.COURIER, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                    var baseBold = BaseFont.CreateFont(BaseFont.COURIER_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                    if (esInformeTecnico)
                    {
                        baseNormal = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                        baseBold = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                    }

                    var tituloFont = new Font(baseNormal, esAocr ? 6.5f : (esInformeTecnico ? 5.2f : 9f), Font.NORMAL, BaseColor.BLACK);
                    var nombreFont = new Font(baseBold, esAocr ? 8.8f : (esInformeTecnico ? 8.8f : 16f), Font.BOLD, BaseColor.BLACK);
                    var detalleFont = new Font(baseNormal, esAocr ? 5.8f : (esInformeTecnico ? 4.8f : 8.5f), Font.NORMAL, BaseColor.BLACK);

                    var ct = new ColumnText(canvas);
                    ct.SetSimpleColumn(rectTexto.Left, rectTexto.Bottom, rectTexto.Right, rectTexto.Top, esAocr ? 7.4f : (esInformeTecnico ? 6.3f : 12f), Element.ALIGN_LEFT);
                    ct.AddText(new Phrase(ObtenerTituloBloqueFirma(rolFirmante), tituloFont));
                    ct.AddText(new Phrase((string.IsNullOrWhiteSpace(nombreFirmante) ? "USUARIO AOCR" : nombreFirmante.Trim().ToUpperInvariant()) + "\n", nombreFont));
                    if (!esAocr && !esInformeTecnico)
                    {
                        ct.AddText(new Phrase("Rol: " + ObtenerEtiquetaRol(rolFirmante) + "\n", detalleFont));
                        ct.AddText(new Phrase("Fecha: " + fechaFirma.ToString("dd/MM/yyyy HH:mm"), detalleFont));
                    }
                    else
                    {
                        ct.AddText(new Phrase(ObtenerEtiquetaRol(rolFirmante) + "\n", detalleFont));
                        ct.AddText(new Phrase("Fecha: " + fechaFirma.ToString("dd/MM/yyyy"), detalleFont));
                    }
                    ct.Go();
                    canvas.RestoreState();
                }

                return output.ToArray();
            }
        }

        private static string ObtenerEtiquetaRol(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "DIRDAC" || rol == "DIRECTOR_GENERAL" || rol == "AOCR_FIRMANTE")
            {
                return "FIRMA AUTORIZADA";
            }

            if (rol == "INFORME_TECNICO_DIRDAC")
            {
                return "FIRMA AUTORIZADA";
            }

            if (rol == "INFORME_TECNICO_INSPECTOR")
            {
                return "INSPECTOR DGAC";
            }

            if (rol == "INSPECTOR")
            {
                return "INSPECTOR";
            }

            return string.IsNullOrWhiteSpace(rol) ? "FIRMANTE" : rol;
        }

        private static string ConstruirContenidoQrPorDefecto(string nombreFirmante, string rolFirmante, string motivo, string ubicacion, X509Certificate certificado, DateTime fechaFirma)
        {
            var sujeto = certificado != null && certificado.SubjectDN != null ? certificado.SubjectDN.ToString() : string.Empty;
            var vigencia = certificado != null ? certificado.NotAfter.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
            return string.Join(" | ", new[]
            {
                "Firmante=" + (nombreFirmante ?? string.Empty),
                "Rol=" + ObtenerRolQr(rolFirmante),
                "Fecha=" + fechaFirma.ToString("yyyy-MM-dd HH:mm:ss"),
                "Motivo=" + (motivo ?? string.Empty),
                "Ubicacion=" + (ubicacion ?? string.Empty),
                "Certificado=" + sujeto,
                "VigenciaHasta=" + vigencia
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
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
