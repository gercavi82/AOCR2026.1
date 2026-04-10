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

        public ResultadoFirmaDigital FirmarPdf(byte[] pdfBytes, byte[] certificadoBytes, string passwordCertificado, string nombreFirmante, string motivo, string ubicacion, string rolFirmante, string contenidoQr = null, PosicionFirmaVisualPdf posicionFirmaVisual = null)
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

                var pdfFuente = EstamparBloqueFirmaVisual(pdfBytes, qrPayload, nombreFirmante, rolFirmante, fechaFirma, posicionFirmaVisual);

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

            // Informe Técnico — columna izquierda (inspector)
            if (rol == "INFORME_TECNICO_INSPECTOR")
            {
                return new Rectangle(30f, 50f, 230f, 146f);
            }

            // Informe Técnico — columna derecha (DIRDAC)
            if (rol == "INFORME_TECNICO_DIRDAC")
            {
                return new Rectangle(300f, 50f, 500f, 146f);
            }

            // Reconocimiento / Condiciones — tercera columna
            if (rol == "AOCR_FIRMANTE")
            {
                return new Rectangle(385f, 50f, 545f, 146f);
            }

            if (rol == "DIRDAC" || rol == "DIRECTOR_GENERAL")
            {
                return new Rectangle(300f, 30f, 565f, 135f);
            }

            return new Rectangle(30f, 30f, 295f, 135f);
        }

        private static Rectangle ObtenerRectanguloQr(string rolFirmante, Rectangle rectanguloFirma = null)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "AOCR_FIRMANTE")
            {
                var totalAocr = rectanguloFirma ?? ObtenerRectanguloFirma(rolFirmante);
                return new Rectangle(totalAocr.Left + 12f, totalAocr.Bottom + 19f, totalAocr.Left + 54f, totalAocr.Bottom + 61f);
            }

            if (rol == "INFORME_TECNICO_INSPECTOR" || rol == "INFORME_TECNICO_DIRDAC")
            {
                var totalInforme = rectanguloFirma ?? ObtenerRectanguloFirma(rolFirmante);
                return new Rectangle(totalInforme.Left + 6f, totalInforme.Bottom + 20f, totalInforme.Left + 46f, totalInforme.Bottom + 60f);
            }

            var total = rectanguloFirma ?? ObtenerRectanguloFirma(rolFirmante);
            return new Rectangle(total.Left + 6f, total.Bottom + 6f, total.Left + 96f, total.Bottom + 96f);
        }

        private static Rectangle ObtenerRectanguloTextoFirma(string rolFirmante, Rectangle rectanguloFirma = null)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "AOCR_FIRMANTE")
            {
                var totalAocr = rectanguloFirma ?? ObtenerRectanguloFirma(rolFirmante);
                return new Rectangle(totalAocr.Left + 62f, totalAocr.Bottom + 16f, totalAocr.Right - 10f, totalAocr.Top - 14f);
            }

            if (rol == "INFORME_TECNICO_INSPECTOR" || rol == "INFORME_TECNICO_DIRDAC")
            {
                var totalInforme = rectanguloFirma ?? ObtenerRectanguloFirma(rolFirmante);
                return new Rectangle(totalInforme.Left + 50f, totalInforme.Bottom + 8f, totalInforme.Right - 4f, totalInforme.Top - 4f);
            }

            var total = rectanguloFirma ?? ObtenerRectanguloFirma(rolFirmante);
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

                return string.Empty;
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

        private static byte[] EstamparBloqueFirmaVisual(byte[] pdfBytes, string contenidoQr, string nombreFirmante, string rolFirmante, DateTime fechaFirma, PosicionFirmaVisualPdf posicionFirmaVisual)
        {
            using (var reader = new PdfReader(pdfBytes))
            using (var output = new MemoryStream())
            {
                using (var stamper = new PdfStamper(reader, output))
                {
                    int pagina;
                    Rectangle rectTotal;
                    string origenPosicion;

                    var ancla = PdfTextAnchorLocator.BuscarAnclaPorRol(reader, rolFirmante);
                    if (ancla != null && ancla.RectanguloFirma != null)
                    {
                        pagina = ancla.Pagina;
                        rectTotal = ancla.RectanguloFirma;
                        origenPosicion = "ANCLA_TEXTO";
                        System.Diagnostics.Trace.WriteLine(string.Format(
                            "[FirmaDigital] Posicion por ancla de texto. Rol={0}, Pagina={1}, Ancla=\"{2}\", Rect=[{3:F1},{4:F1},{5:F1},{6:F1}]",
                            rolFirmante, pagina, ancla.AnclaUsada,
                            rectTotal.Left, rectTotal.Bottom, rectTotal.Right, rectTotal.Top));
                    }
                    else
                    {
                        pagina = ObtenerNumeroPaginaFirma(reader, posicionFirmaVisual);
                        rectTotal = ObtenerRectanguloFirmaPersonalizado(reader, pagina, rolFirmante, posicionFirmaVisual);
                        origenPosicion = posicionFirmaVisual != null && posicionFirmaVisual.EsValida ? "PUNTERO" : "FIJO";
                        System.Diagnostics.Trace.WriteLine(string.Format(
                            "[FirmaDigital] Fallback posicion {0}. Rol={1}, Pagina={2}, Rect=[{3:F1},{4:F1},{5:F1},{6:F1}]. Ancla no encontrada.",
                            origenPosicion, rolFirmante, pagina,
                            rectTotal.Left, rectTotal.Bottom, rectTotal.Right, rectTotal.Top));
                    }
                    var qr = new BarcodeQRCode(contenidoQr, 120, 120, null);
                    var qrImage = qr.GetImage();
                    var rectQr = ObtenerRectanguloQr(rolFirmante, rectTotal);
                    var rectTexto = ObtenerRectanguloTextoFirma(rolFirmante, rectTotal);
                    qrImage.ScaleAbsolute(rectQr.Right - rectQr.Left, rectQr.Top - rectQr.Bottom);
                    qrImage.SetAbsolutePosition(rectQr.Left, rectQr.Bottom);

                    var canvas = stamper.GetOverContent(pagina);
                    canvas.SaveState();
                    var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
                    var esAocr = rol == "AOCR_FIRMANTE";
                    var esFirmaIntegrada = EsFirmaIntegradaEnPlantilla(rolFirmante);
                    var esInformeTecnico = rol == "INFORME_TECNICO_INSPECTOR" || rol == "INFORME_TECNICO_DIRDAC";
                    if (esFirmaIntegrada)
                    {
                        canvas.SetColorFill(BaseColor.WHITE);
                        canvas.Rectangle(rectTotal.Left, rectTotal.Bottom, rectTotal.Right - rectTotal.Left, rectTotal.Top - rectTotal.Bottom);
                        canvas.Fill();
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
                    if (esInformeTecnico || esAocr)
                    {
                        baseNormal = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                        baseBold = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                    }

                    var tituloFont = new Font(baseNormal, esAocr ? 5.4f : (esInformeTecnico ? 5.2f : 9f), Font.NORMAL, BaseColor.BLACK);
                    var nombreFont = new Font(baseBold, esAocr ? 7.0f : (esInformeTecnico ? 8.8f : 16f), Font.BOLD, BaseColor.BLACK);
                    var detalleFont = new Font(baseNormal, esAocr ? 5.2f : (esInformeTecnico ? 4.8f : 8.5f), Font.NORMAL, BaseColor.BLACK);

                    var ct = new ColumnText(canvas);
                    ct.SetSimpleColumn(rectTexto.Left, rectTexto.Bottom, rectTexto.Right, rectTexto.Top, esAocr ? 5.8f : (esInformeTecnico ? 6.3f : 12f), Element.ALIGN_LEFT);
                    var tituloBloque = ObtenerTituloBloqueFirma(rolFirmante);
                    if (!string.IsNullOrWhiteSpace(tituloBloque))
                    {
                        ct.AddText(new Phrase(tituloBloque, tituloFont));
                    }
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

        private static int ObtenerNumeroPaginaFirma(PdfReader reader, PosicionFirmaVisualPdf posicionFirmaVisual)
        {
            if (reader == null)
            {
                return 1;
            }

            if (posicionFirmaVisual != null && posicionFirmaVisual.EsValida)
            {
                return Math.Max(1, Math.Min(reader.NumberOfPages, posicionFirmaVisual.NumeroPagina));
            }

            return reader.NumberOfPages;
        }

        private static Rectangle ObtenerRectanguloFirmaPersonalizado(PdfReader reader, int numeroPagina, string rolFirmante, PosicionFirmaVisualPdf posicionFirmaVisual)
        {
            if (reader != null && posicionFirmaVisual != null && posicionFirmaVisual.EsValida)
            {
                var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
                if (rol == "AOCR_FIRMANTE")
                {
                    var baseRect = ObtenerRectanguloFirma(rolFirmante);
                    var baseWidth = Math.Max(1f, baseRect.Right - baseRect.Left);
                    var baseHeight = Math.Max(1f, baseRect.Top - baseRect.Bottom);
                    var width = Limitar(baseWidth * posicionFirmaVisual.AnchoRatio, 72f, baseWidth);
                    var height = Limitar(baseHeight * posicionFirmaVisual.AltoRatio, 36f, baseHeight);
                    var left = baseRect.Left + Limitar((baseWidth - width) * posicionFirmaVisual.PosicionXRatio, 0f, baseWidth - width);
                    var top = baseRect.Top - Limitar((baseHeight - height) * posicionFirmaVisual.PosicionYRatio, 0f, baseHeight - height);
                    var bottom = top - height;
                    return new Rectangle(left, bottom, left + width, top);
                }

                var pageSize = reader.GetPageSize(numeroPagina);
                var areaUtil = ObtenerAreaUtilFirma(pageSize, rolFirmante);
                var areaWidth = Math.Max(1f, areaUtil.Right - areaUtil.Left);
                var areaHeight = Math.Max(1f, areaUtil.Top - areaUtil.Bottom);
                var rectWidth = Limitar(areaWidth * posicionFirmaVisual.AnchoRatio, 72f, areaWidth * 0.45f);
                var rectHeight = Limitar(areaHeight * posicionFirmaVisual.AltoRatio, 36f, areaHeight * 0.22f);
                var rectLeft = Limitar(areaUtil.Left + (areaWidth * posicionFirmaVisual.PosicionXRatio), areaUtil.Left, areaUtil.Right - rectWidth);
                var rectTop = Limitar(areaUtil.Top - (areaHeight * posicionFirmaVisual.PosicionYRatio), areaUtil.Bottom + rectHeight, areaUtil.Top);
                var rectBottom = rectTop - rectHeight;
                return new Rectangle(rectLeft, rectBottom, rectLeft + rectWidth, rectTop);
            }

            return ObtenerRectanguloFirma(rolFirmante);
        }

        private static Rectangle ObtenerAreaUtilFirma(Rectangle pageSize, string rolFirmante)
        {
            var safePage = pageSize ?? PageSize.A4;
            var role = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (role != "AOCR_FIRMANTE")
            {
                return new Rectangle(0f, 0f, Math.Max(1f, safePage.Width), Math.Max(1f, safePage.Height));
            }

            var left = MmToPoints(8f);
            var right = Math.Max(left + 1f, safePage.Width - MmToPoints(8f));
            var bottom = MmToPoints(26f);
            var top = Math.Max(bottom + 1f, safePage.Height - MmToPoints(30f));
            return new Rectangle(left, bottom, right, top);
        }

        private static float MmToPoints(float millimeters)
        {
            return millimeters * 72f / 25.4f;
        }

        private static float Limitar(float valor, float minimo, float maximo)
        {
            return Math.Max(minimo, Math.Min(maximo, valor));
        }
    }

    public class PosicionFirmaVisualPdf
    {
        public int NumeroPagina { get; set; }
        public float PosicionXRatio { get; set; }
        public float PosicionYRatio { get; set; }
        public float AnchoRatio { get; set; }
        public float AltoRatio { get; set; }

        public bool EsValida
        {
            get
            {
                return NumeroPagina > 0
                    && PosicionXRatio >= 0f && PosicionXRatio < 1f
                    && PosicionYRatio >= 0f && PosicionYRatio < 1f
                    && AnchoRatio > 0f && AnchoRatio < 1f
                    && AltoRatio > 0f && AltoRatio < 1f;
            }
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
