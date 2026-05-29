using System;
using System.IO;
using System.Web;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace CapaNegocio.Helpers
{
    public sealed class PdfBrandingAssets
    {
        public static PdfBrandingAssets Empty(string headerVirtualPath, string footerVirtualPath)
        {
            return new PdfBrandingAssets(headerVirtualPath, footerVirtualPath, null, null, null, null);
        }

        public PdfBrandingAssets(
            string headerVirtualPath,
            string footerVirtualPath,
            string headerPhysicalPath,
            string footerPhysicalPath,
            string headerDataUri,
            string footerDataUri)
        {
            HeaderVirtualPath = headerVirtualPath;
            FooterVirtualPath = footerVirtualPath;
            HeaderPhysicalPath = headerPhysicalPath;
            FooterPhysicalPath = footerPhysicalPath;
            HeaderDataUri = headerDataUri;
            FooterDataUri = footerDataUri;
        }

        public string HeaderVirtualPath { get; private set; }
        public string FooterVirtualPath { get; private set; }
        public string HeaderPhysicalPath { get; private set; }
        public string FooterPhysicalPath { get; private set; }
        public string HeaderDataUri { get; private set; }
        public string FooterDataUri { get; private set; }

        public bool HeaderExists
        {
            get { return !string.IsNullOrWhiteSpace(HeaderPhysicalPath) && File.Exists(HeaderPhysicalPath); }
        }

        public bool FooterExists
        {
            get { return !string.IsNullOrWhiteSpace(FooterPhysicalPath) && File.Exists(FooterPhysicalPath); }
        }
    }

    public static class PdfBrandingHelper
    {
        public const string HeaderVirtualPath = "~/Content/assets/imganes/pdf/header.png";
        public const string FooterVirtualPath = "~/Content/assets/imganes/pdf/footer.png";
        public const string StandardRotativaSwitches = "--enable-local-file-access --print-media-type --background --dpi 300 --zoom 1.0";
        public const string StandardRotativaSwitchesWithBranding = StandardRotativaSwitches + " --disable-smart-shrinking --margin-top 30mm --margin-bottom 26mm --margin-left 8mm --margin-right 8mm --header-spacing 0 --footer-spacing 0";

        private const string ModuleName = "PdfBrandingHelper";

        public static string BuildStandardRotativaSwitches(HttpServerUtility server, string source)
        {
            return BuildStandardRotativaSwitchesInternal(
                source,
                server != null ? (Func<string, string>)server.MapPath : null,
                virtualPath => VirtualPathUtility.ToAbsolute(virtualPath));
        }

        public static string BuildStandardRotativaSwitches(HttpServerUtilityBase server, string source)
        {
            return BuildStandardRotativaSwitchesInternal(
                source,
                server != null ? (Func<string, string>)server.MapPath : null,
                virtualPath => VirtualPathUtility.ToAbsolute(virtualPath));
        }

        public static PdfBrandingAssets ResolveAssets(string source)
        {
            var ctx = HttpContext.Current;
            if (ctx == null || ctx.Server == null)
            {
                LogError(source, "No se pudo resolver HttpContext/Server para cargar header/footer PDF.");
                return PdfBrandingAssets.Empty(HeaderVirtualPath, FooterVirtualPath);
            }

            return ResolveAssets(ctx.Server, source);
        }

        public static PdfBrandingAssets ResolveAssets(HttpServerUtility server, string source)
        {
            if (server == null)
            {
                LogError(source, "No se pudo resolver HttpServerUtility para cargar header/footer PDF.");
                return PdfBrandingAssets.Empty(HeaderVirtualPath, FooterVirtualPath);
            }

            return ResolveAssetsInternal(server.MapPath, source);
        }

        public static PdfBrandingAssets ResolveAssets(HttpServerUtilityBase server, string source)
        {
            if (server == null)
            {
                LogError(source, "No se pudo resolver HttpServerUtilityBase para cargar header/footer PDF.");
                return PdfBrandingAssets.Empty(HeaderVirtualPath, FooterVirtualPath);
            }

            return ResolveAssetsInternal(server.MapPath, source);
        }

        public static PdfHeaderFooterPageEvent CreateITextPageEvent(HttpServerUtility server, string source)
        {
            var assets = ResolveAssets(server, source);
            return new PdfHeaderFooterPageEvent(assets, source);
        }

        public static PdfHeaderFooterPageEvent CreateITextPageEvent(HttpServerUtilityBase server, string source)
        {
            var assets = ResolveAssets(server, source);
            return new PdfHeaderFooterPageEvent(assets, source);
        }

        private static PdfBrandingAssets ResolveAssetsInternal(Func<string, string> mapPath, string source)
        {
            var headerPhysicalPath = SafeMapPath(mapPath, HeaderVirtualPath, source, "header");
            var footerPhysicalPath = SafeMapPath(mapPath, FooterVirtualPath, source, "footer");

            ValidateAsset(HeaderVirtualPath, headerPhysicalPath, source);
            ValidateAsset(FooterVirtualPath, footerPhysicalPath, source);

            var headerDataUri = ToDataUri(headerPhysicalPath);
            var footerDataUri = ToDataUri(footerPhysicalPath);

            return new PdfBrandingAssets(
                HeaderVirtualPath,
                FooterVirtualPath,
                headerPhysicalPath,
                footerPhysicalPath,
                headerDataUri,
                footerDataUri);
        }

        private static string SafeMapPath(Func<string, string> mapPath, string virtualPath, string source, string assetName)
        {
            try
            {
                return mapPath(virtualPath);
            }
            catch (Exception ex)
            {
                LogError(
                    source,
                    string.Format("No se pudo resolver la ruta para {0} ({1}).", assetName, virtualPath),
                    ex);
                return null;
            }
        }

        private static string ToDataUri(string physicalPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(physicalPath) || !File.Exists(physicalPath))
                {
                    return null;
                }

                var extension = Path.GetExtension(physicalPath) ?? string.Empty;
                var mimeType = extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    ? "image/png"
                    : "image/jpeg";

                var base64 = Convert.ToBase64String(File.ReadAllBytes(physicalPath));
                return "data:" + mimeType + ";base64," + base64;
            }
            catch (Exception ex)
            {
                LogError("ToDataUri", "Error al convertir imagen institucional a Data URI.", ex);
                return null;
            }
        }

        private static void ValidateAsset(string virtualPath, string physicalPath, string source)
        {
            if (string.IsNullOrWhiteSpace(physicalPath) || !File.Exists(physicalPath))
            {
                var detail = string.Format(
                    "No se encontro recurso PDF requerido. VirtualPath={0}. PhysicalPath={1}.",
                    virtualPath,
                    string.IsNullOrWhiteSpace(physicalPath) ? "[no-resuelta]" : physicalPath);
                LogError(source, detail);
            }
        }

        private static void LogError(string source, string message, Exception ex = null)
        {
            var finalMessage = string.Format(
                "[PDF-BRANDING] Source={0} | {1}",
                string.IsNullOrWhiteSpace(source) ? "N/A" : source,
                message ?? "Error no especificado.");

            try
            {
                LogBL.RegistrarError(finalMessage, ex != null ? ex.ToString() : null, ModuleName);
            }
            catch
            {
                // Ignorar error de logging para no bloquear la generacion del PDF.
            }

            try
            {
                if (ex == null)
                {
                    System.Diagnostics.Trace.TraceError(finalMessage);
                }
                else
                {
                    System.Diagnostics.Trace.TraceError(finalMessage + " | Exception=" + ex.Message);
                }
            }
            catch
            {
                // Ignorar errores secundarios de trazas.
            }
        }

        private static string BuildStandardRotativaSwitchesInternal(string source, Func<string, string> mapPath, Func<string, string> toAbsoluteVirtualPath)
        {
            var switches = StandardRotativaSwitchesWithBranding;
            if (mapPath == null)
            {
                LogError(source, "No se pudo resolver MapPath para construir el membrete estándar del PDF.");
                return switches;
            }

            try
            {
                var tempFolder = mapPath("~/App_Data/Temp/PdfBranding");
                if (string.IsNullOrWhiteSpace(tempFolder))
                {
                    return switches;
                }

                Directory.CreateDirectory(tempFolder);

                var headerHtmlPath = Path.Combine(tempFolder, "standard_header.html");
                var footerHtmlPath = Path.Combine(tempFolder, "standard_footer.html");

                var headerHtml = BuildStandardHeaderHtml(mapPath, toAbsoluteVirtualPath, source);
                var footerHtml = BuildStandardFooterHtml(mapPath, toAbsoluteVirtualPath, source);

                if (!string.IsNullOrWhiteSpace(headerHtml))
                {
                    File.WriteAllText(headerHtmlPath, headerHtml, Encoding.UTF8);
                    switches += " --header-html \"" + ConvertPhysicalPathToFileUrl(headerHtmlPath) + "\"";
                }

                if (!string.IsNullOrWhiteSpace(footerHtml))
                {
                    File.WriteAllText(footerHtmlPath, footerHtml, Encoding.UTF8);
                    switches += " --footer-html \"" + ConvertPhysicalPathToFileUrl(footerHtmlPath) + "\"";
                }
            }
            catch (Exception ex)
            {
                LogError(source, "No se pudo construir los archivos temporales de branding estándar para wkhtmltopdf.", ex);
            }

            return switches;
        }

        private static string BuildStandardHeaderHtml(Func<string, string> mapPath, Func<string, string> toAbsoluteVirtualPath, string source)
        {
            var barra = ResolveHojaAssetUrl(mapPath, toAbsoluteVirtualPath, "barra.png", source);
            var escudo = ResolveHojaAssetUrl(mapPath, toAbsoluteVirtualPath, "escudo.png", source);
            var dgca = ResolveHojaAssetUrl(mapPath, toAbsoluteVirtualPath, "DGCA.png", source);

            if (string.IsNullOrWhiteSpace(barra) || string.IsNullOrWhiteSpace(escudo) || string.IsNullOrWhiteSpace(dgca))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><style>html,body{{margin:0;padding:0;width:194mm;height:26mm;background:transparent;overflow:hidden;}}.header{{position:relative;width:194mm;height:26mm;}}.barra{{position:absolute;top:0;right:0;width:129mm;height:3.2mm;}}.escudo{{position:absolute;left:0;top:6.2mm;width:34mm;height:auto;}}.dgca{{position:absolute;right:0;top:8.2mm;width:82mm;height:auto;}}</style></head><body><div class=\"header\"><img class=\"barra\" src=\"{0}\" alt=\"\" /><img class=\"escudo\" src=\"{1}\" alt=\"Escudo Republica del Ecuador\" /><img class=\"dgca\" src=\"{2}\" alt=\"Direccion General de Aviacion Civil\" /></div></body></html>",
                HttpUtility.HtmlAttributeEncode(barra),
                HttpUtility.HtmlAttributeEncode(escudo),
                HttpUtility.HtmlAttributeEncode(dgca));
        }

        private static string BuildStandardFooterHtml(Func<string, string> mapPath, Func<string, string> toAbsoluteVirtualPath, string source)
        {
            var barra = ResolveHojaAssetUrl(mapPath, toAbsoluteVirtualPath, "barra.png", source);
            var direccion = ResolveHojaAssetUrl(mapPath, toAbsoluteVirtualPath, "direccion.png", source);
            var nuevo = ResolveHojaAssetUrl(mapPath, toAbsoluteVirtualPath, "nuevo.png", source);

            if (string.IsNullOrWhiteSpace(barra) || string.IsNullOrWhiteSpace(direccion) || string.IsNullOrWhiteSpace(nuevo))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><style>html,body{{margin:0;padding:0;width:194mm;height:26mm;background:transparent;overflow:hidden;}}.footer{{position:relative;width:194mm;height:26mm;}}.barra{{position:absolute;left:0;top:0;width:72mm;height:3.2mm;}}.direccion{{position:absolute;left:0;top:7.2mm;width:64mm;height:auto;}}.nuevo{{position:absolute;right:0;top:6.2mm;width:44mm;height:auto;}}</style></head><body><div class=\"footer\"><img class=\"barra\" src=\"{0}\" alt=\"\" /><img class=\"direccion\" src=\"{1}\" alt=\"Direccion DGAC\" /><img class=\"nuevo\" src=\"{2}\" alt=\"El Nuevo Ecuador\" /></div></body></html>",
                HttpUtility.HtmlAttributeEncode(barra),
                HttpUtility.HtmlAttributeEncode(direccion),
                HttpUtility.HtmlAttributeEncode(nuevo));
        }

        private static string ResolveHojaAssetUrl(Func<string, string> mapPath, Func<string, string> toAbsoluteVirtualPath, string fileName, string source)
        {
            var virtualPath = "~/Content/imganes/hoja/" + fileName;
            try
            {
                var physicalPath = mapPath(virtualPath);
                if (!string.IsNullOrWhiteSpace(physicalPath) && File.Exists(physicalPath))
                {
                    return ConvertPhysicalPathToFileUrl(physicalPath);
                }
            }
            catch (Exception ex)
            {
                LogError(source, "No se pudo resolver el recurso institucional " + virtualPath + ".", ex);
            }

            try
            {
                var absolutePath = toAbsoluteVirtualPath != null ? toAbsoluteVirtualPath(virtualPath) : null;
                return string.IsNullOrWhiteSpace(absolutePath) ? null : absolutePath;
            }
            catch
            {
                return null;
            }
        }

        private static string ConvertPhysicalPathToFileUrl(string physicalPath)
        {
            return "file:///" + (physicalPath ?? string.Empty).Replace('\\', '/');
        }
    }

    public sealed class PdfHeaderFooterPageEvent : PdfPageEventHelper
    {
        private readonly byte[] _headerBytes;
        private readonly byte[] _footerBytes;
        private readonly float _maxHeaderHeight;
        private readonly float _maxFooterHeight;

        public PdfHeaderFooterPageEvent(PdfBrandingAssets assets, string source, float maxHeaderHeight = 100f, float maxFooterHeight = 60f)
        {
            _maxHeaderHeight = maxHeaderHeight;
            _maxFooterHeight = maxFooterHeight;

            try
            {
                if (assets != null && assets.HeaderExists)
                {
                    _headerBytes = File.ReadAllBytes(assets.HeaderPhysicalPath);
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError(
                    "No se pudo cargar header.png para iTextSharp.",
                    ex.ToString(),
                    "PdfHeaderFooterPageEvent");
            }

            try
            {
                if (assets != null && assets.FooterExists)
                {
                    _footerBytes = File.ReadAllBytes(assets.FooterPhysicalPath);
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError(
                    "No se pudo cargar footer.png para iTextSharp.",
                    ex.ToString(),
                    "PdfHeaderFooterPageEvent");
            }
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            if (writer == null || document == null)
            {
                return;
            }

            var canvas = writer.DirectContent;

            if (_headerBytes != null)
            {
                DrawHeader(document, canvas);
            }

            if (_footerBytes != null)
            {
                DrawFooter(document, canvas);
            }
        }

        private void DrawHeader(Document document, PdfContentByte canvas)
        {
            try
            {
                var image = Image.GetInstance(_headerBytes);
                var availableWidth = document.PageSize.Width - document.LeftMargin - document.RightMargin;
                image.ScaleToFit(availableWidth, _maxHeaderHeight);

                var x = document.LeftMargin + (availableWidth - image.ScaledWidth) / 2f;
                var y = document.PageSize.Top - image.ScaledHeight - 10f;

                image.SetAbsolutePosition(x, y);
                canvas.AddImage(image, image.ScaledWidth, 0f, 0f, image.ScaledHeight, x, y);
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError(
                    "No se pudo dibujar header.png en pagina PDF.",
                    ex.ToString(),
                    "PdfHeaderFooterPageEvent");
            }
        }

        private void DrawFooter(Document document, PdfContentByte canvas)
        {
            try
            {
                var image = Image.GetInstance(_footerBytes);
                var availableWidth = document.PageSize.Width - document.LeftMargin - document.RightMargin;
                image.ScaleToFit(availableWidth, _maxFooterHeight);

                var x = document.LeftMargin + (availableWidth - image.ScaledWidth) / 2f;
                var y = 10f;

                image.SetAbsolutePosition(x, y);
                canvas.AddImage(image, image.ScaledWidth, 0f, 0f, image.ScaledHeight, x, y);
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError(
                    "No se pudo dibujar footer.png en pagina PDF.",
                    ex.ToString(),
                    "PdfHeaderFooterPageEvent");
            }
        }
    }
}
