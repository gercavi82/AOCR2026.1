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

                RegistrarDiagnosticoITextSharp("SIGN_IN", rolFirmante);
                EnsureITextVersionInitialized();

                var fechaFirma = DateTime.Now;
                var qrPayload = !string.IsNullOrWhiteSpace(contenidoQr)
                    ? contenidoQr
                    : ConstruirContenidoQrPorDefecto(nombreFirmante, rolFirmante, motivo, ubicacion, certificado, fechaFirma);

                var pdfFuente = EstamparBloqueFirmaVisual(
                    pdfBytes,
                    qrPayload,
                    nombreFirmante,
                    rolFirmante,
                    fechaFirma,
                    posicionFirmaVisual,
                    motivo,
                    ubicacion,
                    certificado.SubjectDN != null ? certificado.SubjectDN.ToString() : null);

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
                        RegistrarDiagnosticoITextSharp("SIGN_OK", rolFirmante);
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
                System.Diagnostics.Trace.TraceError("[FirmaDigital][ERROR] " + ObtenerDetalleExcepcion(ex));
                return ResultadoFirmaDigital.Error("No se pudo aplicar la firma digital al PDF: " + ObtenerDetalleExcepcion(ex));
            }
        }

        private static Rectangle ObtenerRectanguloFirma(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();

            // Informe Técnico — columna izquierda (inspector)
            // Centrado dentro del slot de ~260pt de ancho, 90pt de alto
            if (rol == "INFORME_TECNICO_INSPECTOR")
            {
                return new Rectangle(32f, 52f, 262f, 142f);
            }

            if (rol == "LV_EAE_INSPECTOR")
            {
                return new Rectangle(162f, 80f, 430f, 148f);
            }

            // Informe Técnico — columna derecha (DIRDAC)
            if (rol == "INFORME_TECNICO_DIRDAC")
            {
                return new Rectangle(298f, 52f, 528f, 142f);
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
            var total = rectanguloFirma ?? ObtenerRectanguloFirma(rolFirmante);
            var totalW = Math.Max(1f, total.Right - total.Left);
            var totalH = Math.Max(1f, total.Top - total.Bottom);
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            var esInformeTecnico = rol == "INFORME_TECNICO_INSPECTOR" || rol == "INFORME_TECNICO_DIRDAC";
            var esCertificado = rol == "DIRDAC" || rol == "DIRECTOR_GENERAL";

            // En informe técnico se usa un QR dominante, casi a toda la altura del bloque.
            // En las demás plantillas se mantiene la ubicación heredada.
            var qrSize = esInformeTecnico
                ? Math.Min(totalH * 0.82f, totalW * 0.34f)
                : (esCertificado
                    ? Math.Min(totalH * 0.38f, totalW * 0.28f)
                    : Math.Min(totalH * 0.45f, totalW * 0.35f));
            qrSize = Math.Max(qrSize, 32f); // mínimo 32pt
            var padding = esCertificado ? Math.Max(3f, totalH * 0.04f) : Math.Max(4f, totalH * 0.05f);
            var qrBottom = esInformeTecnico
                ? total.Bottom + Math.Max(padding, (totalH - qrSize) / 2f)
                : (esCertificado
                    ? total.Bottom + Math.Max(padding + 1f, (totalH - qrSize) / 2f)
                    : total.Bottom + padding);

            return new Rectangle(
                total.Left + padding,
                qrBottom,
                total.Left + padding + qrSize,
                qrBottom + qrSize);
        }

        private static Rectangle ObtenerRectanguloTextoFirma(string rolFirmante, Rectangle rectanguloFirma = null)
        {
            var total = rectanguloFirma ?? ObtenerRectanguloFirma(rolFirmante);
            var totalH = Math.Max(1f, total.Top - total.Bottom);
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            var esInformeTecnico = rol == "INFORME_TECNICO_INSPECTOR" || rol == "INFORME_TECNICO_DIRDAC";
            var esCertificado = rol == "DIRDAC" || rol == "DIRECTOR_GENERAL";

            // Texto: a la derecha del QR, ocupa el resto del ancho
            var qrRect = ObtenerRectanguloQr(rolFirmante, total);
            var leftMargin = qrRect.Right + (esInformeTecnico ? 12f : (esCertificado ? 6f : 4f));
            var padding = esCertificado ? Math.Max(3f, totalH * 0.04f) : Math.Max(4f, totalH * 0.05f);

            return new Rectangle(
                leftMargin,
                total.Bottom + (esInformeTecnico ? padding + 2f : (esCertificado ? padding + 1f : padding)),
                total.Right - (esCertificado ? padding + 2f : padding),
                total.Top - (esInformeTecnico ? padding + 2f : (esCertificado ? padding + 1f : padding)));
        }

        private static bool EsFirmaIntegradaEnPlantilla(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            return rol == "AOCR_FIRMANTE"
                || rol == "INFORME_TECNICO_INSPECTOR"
                || rol == "LV_EAE_INSPECTOR"
                || rol == "INFORME_TECNICO_DIRDAC"
                || rol == "DIRDAC"
                || rol == "DIRECTOR_GENERAL";
        }

        private static string ObtenerTituloBloqueFirma(string rolFirmante)
        {
            var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "INFORME_TECNICO_INSPECTOR")
            {
                return "Firmado electronicamente por:\n";
            }

            if (rol == "LV_EAE_INSPECTOR")
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

            if (rol == "LV_EAE_INSPECTOR")
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
            try
            {
                var assembly = typeof(PdfReader).Assembly;
                var version = assembly.GetName().Version;
                var versionTexto = version != null ? version.ToString() : string.Empty;
                var location = assembly.Location ?? string.Empty;

                if (!versionTexto.StartsWith("5.5.13.5", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Version iTextSharp no compatible para firma digital AOCR. Se esperaba 5.5.13.5 y se cargo " + versionTexto + " desde " + location + ".");
                }

                var versionType = typeof(iTextSharp.text.Version);
                var versionField = versionType.GetField("version", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (versionField != null && versionField.GetValue(null) == null)
                {
                    var constructor = versionType.GetConstructor(Type.EmptyTypes);
                    if (constructor != null)
                    {
                        versionField.SetValue(null, constructor.Invoke(null));
                    }
                }

                iTextSharp.text.Version.GetInstance();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("No se pudo inicializar iTextSharp 5.5.13.5 para firma digital. Revise que no exista itextsharp.dll antiguo en bin/Temporary ASP.NET Files. Detalle: " + ObtenerDetalleExcepcion(ex), ex);
            }
        }

        private static void RegistrarDiagnosticoITextSharp(string etapa, string rolFirmante)
        {
            try
            {
                var assembly = typeof(PdfReader).Assembly;
                var version = assembly.GetName().Version;
                System.Diagnostics.Trace.TraceInformation(string.Format(
                    "[FirmaDigital][{0}] Rol={1}, iTextSharpVersion={2}, iTextSharpLocation={3}",
                    etapa,
                    rolFirmante ?? string.Empty,
                    version != null ? version.ToString() : string.Empty,
                    assembly.Location ?? string.Empty));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[FirmaDigital][" + etapa + "] No se pudo registrar diagnostico iTextSharp. " + ObtenerDetalleExcepcion(ex));
            }
        }

        private static string ObtenerDetalleExcepcion(Exception ex)
        {
            if (ex == null)
            {
                return string.Empty;
            }

            var detalles = ex.GetType().FullName + ": " + ex.Message;
            var inner = ex.InnerException;
            while (inner != null)
            {
                detalles += " | Inner: " + inner.GetType().FullName + ": " + inner.Message;
                inner = inner.InnerException;
            }

            return detalles;
        }

        private static byte[] EstamparBloqueFirmaVisual(byte[] pdfBytes, string contenidoQr, string nombreFirmante, string rolFirmante, DateTime fechaFirma, PosicionFirmaVisualPdf posicionFirmaVisual, string motivoFirma, string ubicacionFirma, string sujetoCertificado)
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
                    var canvas = stamper.GetOverContent(pagina);
                    canvas.SaveState();
                    var rol = (rolFirmante ?? string.Empty).Trim().ToUpperInvariant();
                    var esAocr = rol == "AOCR_FIRMANTE";
                    var esCertificado = rol == "DIRDAC" || rol == "DIRECTOR_GENERAL";
                    var esFirmaIntegrada = EsFirmaIntegradaEnPlantilla(rolFirmante);
                    var esInformeTecnico = rol == "INFORME_TECNICO_INSPECTOR" || rol == "INFORME_TECNICO_DIRDAC" || rol == "LV_EAE_INSPECTOR";
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

                    if (esInformeTecnico)
                    {
                        DibujarTarjetaInformeTecnico(canvas, rectTotal, nombreFirmante, rolFirmante, fechaFirma, motivoFirma, ubicacionFirma, sujetoCertificado);
                    }
                    else
                    {
                        var qr = new BarcodeQRCode(contenidoQr, 120, 120, null);
                        var qrImage = qr.GetImage();
                        var rectQr = ObtenerRectanguloQr(rolFirmante, rectTotal);
                        var rectTexto = ObtenerRectanguloTextoFirma(rolFirmante, rectTotal);
                        qrImage.ScaleAbsolute(rectQr.Right - rectQr.Left, rectQr.Top - rectQr.Bottom);
                        qrImage.SetAbsolutePosition(rectQr.Left, rectQr.Bottom);
                        canvas.AddImage(qrImage);

                        var baseNormal = BaseFont.CreateFont(BaseFont.COURIER, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                        var baseBold = BaseFont.CreateFont(BaseFont.COURIER_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                        if (esAocr)
                        {
                            baseNormal = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                            baseBold = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                        }
                        else if (esCertificado)
                        {
                            baseNormal = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                            baseBold = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                        }

                        var tituloFont = new Font(baseNormal, esAocr ? 5.4f : (esCertificado ? 5.6f : 9f), Font.NORMAL, BaseColor.BLACK);
                        var nombreFont = new Font(baseBold, esAocr ? 7.0f : (esCertificado ? 8.2f : 16f), Font.BOLD, BaseColor.BLACK);
                        var detalleFont = new Font(baseNormal, esAocr ? 5.2f : (esCertificado ? 5.9f : 8.5f), Font.NORMAL, BaseColor.BLACK);

                        var ct = new ColumnText(canvas);
                        ct.SetSimpleColumn(rectTexto.Left, rectTexto.Bottom, rectTexto.Right, rectTexto.Top, esAocr ? 5.8f : (esCertificado ? 7.2f : 12f), Element.ALIGN_LEFT);
                        var tituloBloque = ObtenerTituloBloqueFirma(rolFirmante);
                        if (!string.IsNullOrWhiteSpace(tituloBloque))
                        {
                            ct.AddText(new Phrase(tituloBloque, tituloFont));
                        }
                        var nombreVisual = string.IsNullOrWhiteSpace(nombreFirmante)
                            ? "USUARIO AOCR"
                            : (esCertificado ? nombreFirmante.Trim() : nombreFirmante.Trim().ToUpperInvariant());
                        ct.AddText(new Phrase(nombreVisual + "\n", nombreFont));
                        if (!esAocr)
                        {
                            if (esCertificado)
                            {
                                ct.AddText(new Phrase(ObtenerEtiquetaRol(rolFirmante) + "\n", detalleFont));
                                ct.AddText(new Phrase("Fecha: " + fechaFirma.ToString("dd/MM/yyyy HH:mm"), detalleFont));
                            }
                            else
                            {
                                ct.AddText(new Phrase("Rol: " + ObtenerEtiquetaRol(rolFirmante) + "\n", detalleFont));
                                ct.AddText(new Phrase("Fecha: " + fechaFirma.ToString("dd/MM/yyyy HH:mm"), detalleFont));
                            }
                        }
                        else
                        {
                            ct.AddText(new Phrase(ObtenerEtiquetaRol(rolFirmante) + "\n", detalleFont));
                            ct.AddText(new Phrase("Fecha: " + fechaFirma.ToString("dd/MM/yyyy"), detalleFont));
                        }
                        ct.Go();
                    }
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

            if (rol == "LV_EAE_INSPECTOR")
            {
                return "TECNICO / INSPECTOR DGAC";
            }

            if (rol == "INSPECTOR")
            {
                return "INSPECTOR";
            }

            return string.IsNullOrWhiteSpace(rol) ? "FIRMANTE" : rol;
        }

        private static void DibujarTarjetaInformeTecnico(PdfContentByte canvas, Rectangle rectTotal, string nombreFirmante, string rolFirmante, DateTime fechaFirma, string motivoFirma, string ubicacionFirma, string sujetoCertificado)
        {
            var esLvEae = string.Equals((rolFirmante ?? string.Empty).Trim(), "LV_EAE_INSPECTOR", StringComparison.OrdinalIgnoreCase);
            var fondo = new BaseColor(249, 249, 249);
            var borde = new BaseColor(205, 205, 205);
            var divisor = new BaseColor(220, 220, 220);
            var tituloColor = new BaseColor(90, 90, 90);

            canvas.SetColorFill(fondo);
            canvas.Rectangle(rectTotal.Left, rectTotal.Bottom, rectTotal.Right - rectTotal.Left, rectTotal.Top - rectTotal.Bottom);
            canvas.Fill();

            canvas.SetColorStroke(borde);
            canvas.SetLineWidth(0.8f);
            canvas.Rectangle(rectTotal.Left, rectTotal.Bottom, rectTotal.Right - rectTotal.Left, rectTotal.Top - rectTotal.Bottom);
            canvas.Stroke();

            var width = rectTotal.Right - rectTotal.Left;
            var paddingX = esLvEae ? 8f : 10f;
            var paddingY = esLvEae ? 6f : 8f;
            var splitX = rectTotal.Left + (width * (esLvEae ? 0.46f : 0.43f));

            canvas.SetColorStroke(divisor);
            canvas.SetLineWidth(0.6f);
            canvas.MoveTo(splitX, rectTotal.Bottom + 6f);
            canvas.LineTo(splitX, rectTotal.Top - 6f);
            canvas.Stroke();

            var leftRect = new Rectangle(
                rectTotal.Left + paddingX,
                rectTotal.Bottom + paddingY,
                splitX - 8f,
                rectTotal.Top - paddingY);

            var rightRect = new Rectangle(
                splitX + 8f,
                rectTotal.Bottom + paddingY,
                rectTotal.Right - paddingX,
                rectTotal.Top - paddingY);

            var nombreMostrado = string.IsNullOrWhiteSpace(nombreFirmante) ? "Usuario AOCR" : nombreFirmante.Trim();
            var detalleCertificado = string.IsNullOrWhiteSpace(sujetoCertificado) ? null : sujetoCertificado.Trim();
            if (!string.IsNullOrWhiteSpace(detalleCertificado) && detalleCertificado.Length > 92)
            {
                detalleCertificado = detalleCertificado.Substring(0, 89) + "...";
            }

            var nameFont = new Font(Font.FontFamily.HELVETICA, esLvEae ? 14.2f : 17f, Font.BOLD, BaseColor.BLACK);
            var titleFont = new Font(Font.FontFamily.HELVETICA, esLvEae ? 6.0f : 6.8f, Font.NORMAL, tituloColor);
            var detailFont = new Font(Font.FontFamily.HELVETICA, esLvEae ? 6.4f : 7.1f, Font.NORMAL, BaseColor.BLACK);

            var nameColumn = new ColumnText(canvas);
            nameColumn.SetSimpleColumn(leftRect.Left, leftRect.Bottom, leftRect.Right, leftRect.Top, esLvEae ? 14.8f : 18f, Element.ALIGN_LEFT);
            nameColumn.AddText(new Phrase(nombreMostrado, nameFont));
            nameColumn.Go();

            var detailColumn = new ColumnText(canvas);
            detailColumn.SetSimpleColumn(rightRect.Left, rightRect.Bottom, rightRect.Right, rightRect.Top, esLvEae ? 7.8f : 9.2f, Element.ALIGN_LEFT);
            detailColumn.AddText(new Phrase("Firmado digitalmente por\n", titleFont));
            detailColumn.AddText(new Phrase(nombreMostrado + "\n", detailFont));
            detailColumn.AddText(new Phrase("Rol: " + ObtenerEtiquetaRol(rolFirmante) + "\n", detailFont));

            if (!string.IsNullOrWhiteSpace(ubicacionFirma))
            {
                detailColumn.AddText(new Phrase("Sistema: " + ubicacionFirma.Trim() + "\n", detailFont));
            }

            if (!string.IsNullOrWhiteSpace(motivoFirma))
            {
                detailColumn.AddText(new Phrase("Motivo: " + motivoFirma.Trim() + "\n", detailFont));
            }

            if (!string.IsNullOrWhiteSpace(detalleCertificado))
            {
                detailColumn.AddText(new Phrase("Certificado: " + detalleCertificado + "\n", detailFont));
            }

            detailColumn.AddText(new Phrase("Fecha: " + fechaFirma.ToString("dd/MM/yyyy HH:mm:ss"), detailFont));
            detailColumn.Go();
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
