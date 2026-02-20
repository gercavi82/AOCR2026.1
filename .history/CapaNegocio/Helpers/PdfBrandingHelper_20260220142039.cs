using System;
using System.IO;
using System.Web;
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
        public const string StandardRotativaSwitches = "--print-media-type --background --enable-local-file-access --disable-smart-shrinking --dpi 96 --zoom 1.0";

        private const string ModuleName = "PdfBrandingHelper";

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
                if (string.IsNullOrWhiteSpace(physicalPath))
                {
                    LogError("ToDataUri", $"physicalPath es null o vacío");
                    return null;
                }
                if (!File.Exists(physicalPath))
                {
                    LogError("ToDataUri", $"No existe el archivo: {physicalPath}");
                    return null;
                }

                var extension = Path.GetExtension(physicalPath) ?? string.Empty;
                var mimeType = extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    ? "image/png"
                    : "image/jpeg";

                var base64 = Convert.ToBase64String(File.ReadAllBytes(physicalPath));
                var dataUri = "data:" + mimeType + ";base64," + base64;
                LogError("ToDataUri", $"DataUri generado correctamente para {physicalPath}, longitud: {dataUri.Length}");
                return dataUri;
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
