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

    public sealed class PdfHojaBrandingAssets
    {
        public PdfHojaBrandingAssets(
            string barraDataUri,
            string escudoDataUri,
            string dgcaDataUri,
            string direccionDataUri,
            string nuevoDataUri)
        {
            BarraDataUri = barraDataUri;
            EscudoDataUri = escudoDataUri;
            DgcaDataUri = dgcaDataUri;
            DireccionDataUri = direccionDataUri;
            NuevoDataUri = nuevoDataUri;
        }

        public string BarraDataUri { get; private set; }
        public string EscudoDataUri { get; private set; }
        public string DgcaDataUri { get; private set; }
        public string DireccionDataUri { get; private set; }
        public string NuevoDataUri { get; private set; }

        public bool TieneHeaderCompleto
        {
            get
            {
                return !string.IsNullOrWhiteSpace(BarraDataUri)
                    && !string.IsNullOrWhiteSpace(EscudoDataUri)
                    && !string.IsNullOrWhiteSpace(DgcaDataUri);
            }
        }

        public bool TieneFooterCompleto
        {
            get
            {
                return !string.IsNullOrWhiteSpace(BarraDataUri)
                    && !string.IsNullOrWhiteSpace(DireccionDataUri)
                    && !string.IsNullOrWhiteSpace(NuevoDataUri);
            }
        }
    }

    public static class PdfBrandingHelper
    {
        public const string HeaderVirtualPath = "~/Content/assets/imganes/pdf/header.png";
        public const string FooterVirtualPath = "~/Content/assets/imganes/pdf/footer.png";
        public const string LetterheadVirtualPath = "~/Content/imganes/hoja/Hoja_membretada_DGAC_2025.pdf";
        public const string StandardRotativaSwitches = "--enable-local-file-access --print-media-type --background --dpi 300 --zoom 1.0";
        public const string StandardRotativaSwitchesWithBranding = StandardRotativaSwitches + " --disable-smart-shrinking --margin-top 30mm --margin-bottom 26mm --margin-left 8mm --margin-right 8mm --header-spacing 0 --footer-spacing 0";
        public const string StandardRotativaSwitchesInlineBranding = StandardRotativaSwitches + " --disable-smart-shrinking";

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

        public static PdfHojaBrandingAssets ResolveHojaAssets(string source)
        {
            var ctx = HttpContext.Current;
            if (ctx == null || ctx.Server == null)
            {
                LogError(source, "No se pudo resolver HttpContext/Server para cargar assets de hoja PDF.");
                return new PdfHojaBrandingAssets(null, null, null, null, null);
            }

            return ResolveHojaAssets(ctx.Server, source);
        }

        public static PdfHojaBrandingAssets ResolveHojaAssets(HttpServerUtility server, string source)
        {
            if (server == null)
            {
                LogError(source, "No se pudo resolver HttpServerUtility para cargar assets de hoja PDF.");
                return new PdfHojaBrandingAssets(null, null, null, null, null);
            }

            return ResolveHojaAssetsInternal(server.MapPath, source);
        }

        public static PdfHojaBrandingAssets ResolveHojaAssets(HttpServerUtilityBase server, string source)
        {
            if (server == null)
            {
                LogError(source, "No se pudo resolver HttpServerUtilityBase para cargar assets de hoja PDF.");
                return new PdfHojaBrandingAssets(null, null, null, null, null);
            }

            return ResolveHojaAssetsInternal(server.MapPath, source);
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

        public static byte[] ApplyLetterheadBackground(byte[] pdfBytes, HttpServerUtilityBase server, string source)
        {
            return ApplyLetterheadBackgroundInternal(
                pdfBytes,
                server != null ? (Func<string, string>)server.MapPath : null,
                source);
        }

        public static byte[] ApplyLetterheadBackground(byte[] pdfBytes, HttpServerUtility server, string source)
        {
            return ApplyLetterheadBackgroundInternal(
                pdfBytes,
                server != null ? (Func<string, string>)server.MapPath : null,
                source);
        }

        private static PdfBrandingAssets ResolveAssetsInternal(Func<string, string> mapPath, string source)
        {
            var headerPhysicalPath = SafeMapPath(mapPath, HeaderVirtualPath, source, "header");
            var footerPhysicalPath = SafeMapPath(mapPath, FooterVirtualPath, source, "footer");

            ValidateAsset(HeaderVirtualPath, headerPhysicalPath, source);
            ValidateAsset(FooterVirtualPath, footerPhysicalPath, source);

            var headerDataUri = ToDataUri(headerPhysicalPath, HeaderVirtualPath, source, "header.png");
            var footerDataUri = ToDataUri(footerPhysicalPath, FooterVirtualPath, source, "footer.png");

            return new PdfBrandingAssets(
                HeaderVirtualPath,
                FooterVirtualPath,
                headerPhysicalPath,
                footerPhysicalPath,
                headerDataUri,
                footerDataUri);
        }

        private static byte[] ApplyLetterheadBackgroundInternal(byte[] pdfBytes, Func<string, string> mapPath, string source)
        {
            LogInfo(
                source,
                string.Format(
                    "Inicio aplicacion hoja membretada. PdfBytes={0}. MotorPdf=itextsharp. Ensamblado={1}.",
                    pdfBytes == null ? 0 : pdfBytes.Length,
                    GetITextSharpAssemblyInfo()));

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                return ReturnOriginalPdf(source, pdfBytes, "No se recibieron bytes del PDF para aplicar hoja membretada.");
            }

            if (mapPath == null)
            {
                return ReturnOriginalPdf(source, pdfBytes, "No se pudo resolver MapPath para aplicar hoja membretada.");
            }

            var letterheadPhysicalPath = SafeMapPath(mapPath, LetterheadVirtualPath, source, "Hoja_membretada_DGAC_2025.pdf");
            var letterheadExists = !string.IsNullOrWhiteSpace(letterheadPhysicalPath) && File.Exists(letterheadPhysicalPath);
            var letterheadLength = letterheadExists ? new FileInfo(letterheadPhysicalPath).Length : 0L;

            LogInfo(
                source,
                string.Format(
                    "Hoja membretada PDF. VirtualPath={0}. PhysicalPath={1}. Exists={2}. Length={3}.",
                    LetterheadVirtualPath,
                    string.IsNullOrWhiteSpace(letterheadPhysicalPath) ? "[no-resuelta]" : letterheadPhysicalPath,
                    letterheadExists,
                    letterheadLength));

            if (!letterheadExists)
            {
                return ReturnOriginalPdf(
                    source,
                    pdfBytes,
                    string.Format(
                        "No se encontro la hoja membretada PDF requerida. VirtualPath={0}. PhysicalPath={1}.",
                        LetterheadVirtualPath,
                        string.IsNullOrWhiteSpace(letterheadPhysicalPath) ? "[no-resuelta]" : letterheadPhysicalPath));
            }

            if (letterheadLength <= 0L)
            {
                return ReturnOriginalPdf(
                    source,
                    pdfBytes,
                    string.Format(
                        "La hoja membretada PDF esta vacia. PhysicalPath={0}. Length={1}.",
                        letterheadPhysicalPath,
                        letterheadLength));
            }

            try
            {
                using (var output = new MemoryStream())
                using (var sourceReader = new PdfReader(pdfBytes))
                using (var letterheadReader = new PdfReader(letterheadPhysicalPath))
                {
                    var pageCount = sourceReader.NumberOfPages;
                    var letterheadPageCount = letterheadReader.NumberOfPages;

                    using (var stamper = new PdfStamper(sourceReader, output))
                    {
                        if (pageCount <= 0)
                        {
                            return ReturnOriginalPdf(source, pdfBytes, "El PDF fuente no tiene páginas para aplicar hoja membretada.");
                        }

                        if (letterheadPageCount <= 0)
                        {
                            return ReturnOriginalPdf(source, pdfBytes, "La hoja membretada no tiene páginas.");
                        }

                        var letterheadSize = letterheadReader.GetPageSizeWithRotation(1);

                        LogInfo(source, string.Format("[DIAG] letterheadSize: {0} x {1}, MediaBox: {2}, CropBox: {3}", 
                            letterheadSize.Width, letterheadSize.Height,
                            letterheadReader.GetPageN(1) != null ? letterheadReader.GetPageN(1).GetAsArray(PdfName.MEDIABOX)?.ToString() : "null",
                            letterheadReader.GetPageN(1) != null ? letterheadReader.GetPageN(1).GetAsArray(PdfName.CROPBOX)?.ToString() : "null"));

                        for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
                        {
                            var pageSize = sourceReader.GetPageSizeWithRotation(pageNumber);

                            LogInfo(source, string.Format("[DIAG] Page {0} Size: {1} x {2}, MediaBox: {3}, CropBox: {4}",
                                pageNumber, pageSize.Width, pageSize.Height,
                                sourceReader.GetPageN(pageNumber) != null ? sourceReader.GetPageN(pageNumber).GetAsArray(PdfName.MEDIABOX)?.ToString() : "null",
                                sourceReader.GetPageN(pageNumber) != null ? sourceReader.GetPageN(pageNumber).GetAsArray(PdfName.CROPBOX)?.ToString() : "null"));

                            var scaleX = pageSize.Width / letterheadSize.Width;
                            var scaleY = pageSize.Height / letterheadSize.Height;

                            var letterheadForm = stamper.GetImportedPage(letterheadReader, 1);

                            var canvas = stamper.GetOverContent(pageNumber);

                            canvas.AddTemplate(
                                letterheadForm,
                                scaleX,
                                0f,
                                0f,
                                scaleY,
                                pageSize.Left,
                                pageSize.Bottom);
                        }
                    }

                    var stampedBytes = output.ToArray();

                    if (stampedBytes == null || stampedBytes.Length == 0)
                    {
                        return ReturnOriginalPdf(source, pdfBytes, "La aplicación de hoja membretada generó un PDF vacío.");
                    }

                    LogInfo(
                        source,
                        string.Format(
                            "Hoja membretada aplicada correctamente con iTextSharp. PdfOriginalBytes={0}. PdfFinalBytes={1}.",
                            pdfBytes.Length,
                            stampedBytes.Length));

                    return stampedBytes;
                }
            }
            catch (Exception ex)
            {
                return ReturnOriginalPdf(source, pdfBytes, "No se pudo aplicar la hoja membretada al PDF final (iTextSharp).", ex);
            }
        }

        private static byte[] ReturnOriginalPdf(string source, byte[] pdfBytes, string reason, Exception ex = null)
        {
            LogError(
                source,
                string.Format(
                    "{0} Resultado=Se devuelve PDF original. PdfOriginalBytes={1}.",
                    string.IsNullOrWhiteSpace(reason) ? "No se pudo aplicar hoja membretada." : reason,
                    pdfBytes == null ? 0 : pdfBytes.Length),
                ex);

            return pdfBytes;
        }

        private static string GetITextSharpAssemblyInfo()
        {
            try
            {
                var assembly = typeof(PdfReader).Assembly;
                var location = assembly.Location;
                var fileVersion = string.Empty;

                try
                {
                    if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
                    {
                        fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(location).FileVersion;
                    }
                }
                catch
                {
                    fileVersion = "[no-disponible]";
                }

                return string.Format(
                    "Name={0}; AssemblyVersion={1}; FileVersion={2}; Location={3}",
                    assembly.GetName().Name,
                    assembly.GetName().Version,
                    string.IsNullOrWhiteSpace(fileVersion) ? "[no-disponible]" : fileVersion,
                    string.IsNullOrWhiteSpace(location) ? "[sin-location]" : location);
            }
            catch (Exception ex)
            {
                return "No se pudo obtener informacion de itextsharp.dll: " + ex.Message;
            }
        }

        private static PdfHojaBrandingAssets ResolveHojaAssetsInternal(Func<string, string> mapPath, string source)
        {
            return new PdfHojaBrandingAssets(
                ResolveHojaAssetDataUri(mapPath, "barra.png", source),
                ResolveHojaAssetDataUri(mapPath, "escudo.png", source),
                ResolveHojaAssetDataUri(mapPath, "DGCA.png", source),
                ResolveHojaAssetDataUri(mapPath, "direccion.png", source),
                ResolveHojaAssetDataUri(mapPath, "nuevo.png", source));
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

        private static string ToDataUri(string physicalPath, string virtualPath, string source, string assetName)
        {
            try
            {
                var exists = !string.IsNullOrWhiteSpace(physicalPath) && File.Exists(physicalPath);
                var length = exists ? new FileInfo(physicalPath).Length : 0L;

                LogInfo(
                    source,
                    string.Format(
                        "Recurso grafico PDF. Asset={0}. VirtualPath={1}. PhysicalPath={2}. Exists={3}. Length={4}.",
                        assetName,
                        virtualPath,
                        string.IsNullOrWhiteSpace(physicalPath) ? "[no-resuelta]" : physicalPath,
                        exists,
                        length));

                if (!exists)
                {
                    LogError(
                        source,
                        string.Format(
                            "No se encontro recurso grafico PDF requerido. Asset={0}. VirtualPath={1}. PhysicalPath={2}.",
                            assetName,
                            virtualPath,
                            string.IsNullOrWhiteSpace(physicalPath) ? "[no-resuelta]" : physicalPath));
                    return null;
                }

                var dataUri = ToDataUri(physicalPath);
                LogInfo(
                    source,
                    string.Format(
                        "Conversion base64 recurso grafico PDF. Asset={0}. Base64Ok={1}. DataUriLength={2}.",
                        assetName,
                        !string.IsNullOrWhiteSpace(dataUri),
                        string.IsNullOrWhiteSpace(dataUri) ? 0 : dataUri.Length));

                return dataUri;
            }
            catch (Exception ex)
            {
                LogError(source, "Error al validar o convertir recurso grafico PDF " + assetName + ".", ex);
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
                    System.Diagnostics.Trace.TraceError(finalMessage + " | Exception=" + ex.ToString());
                }
            }
            catch
            {
                // Ignorar errores secundarios
            }
        }

        private static void LogInfo(string source, string message)
        {
            var finalMessage = string.Format(
                "[PDF-BRANDING] Source={0} | {1}",
                string.IsNullOrWhiteSpace(source) ? "N/A" : source,
                message ?? "Info no especificada.");

            try
            {
                LogBL.RegistrarInfo(finalMessage, ModuleName);
            }
            catch
            {
                // Ignorar error de logging para no bloquear la generacion del PDF.
            }

            try
            {
                System.Diagnostics.Trace.TraceInformation(finalMessage);
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
            var barra = ResolveHojaAssetDataUri(mapPath, "barra.png", source);
            var escudo = ResolveHojaAssetDataUri(mapPath, "escudo.png", source);
            var dgca = ResolveHojaAssetDataUri(mapPath, "DGCA.png", source);

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
            var barra = ResolveHojaAssetDataUri(mapPath, "barra.png", source);
            var direccion = ResolveHojaAssetDataUri(mapPath, "direccion.png", source);
            var nuevo = ResolveHojaAssetDataUri(mapPath, "nuevo.png", source);

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

        private static string ResolveHojaAssetDataUri(Func<string, string> mapPath, string fileName, string source)
        {
            var virtualPath = "~/Content/imganes/hoja/" + fileName;
            try
            {
                var physicalPath = mapPath(virtualPath);
                return ToDataUri(physicalPath, virtualPath, source, fileName);
            }
            catch (Exception ex)
            {
                LogError(source, "No se pudo resolver el recurso institucional " + virtualPath + ".", ex);
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
