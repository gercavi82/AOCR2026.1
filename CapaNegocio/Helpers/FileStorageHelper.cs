using System;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Web;
using CapaUtilidades;

namespace CapaNegocio.Helpers
{
    public static class FileStorageHelper
    {
        public static int MaxFileSizeMb
        {
            get
            {
                var raw = ConfigurationManager.AppSettings["RT_MaxFileSizeMb"];
                if (int.TryParse(raw, out var mb) && mb > 0)
                {
                    return mb;
                }
                return 10;
            }
        }

        public static string BasePathStorage
        {
            get
            {
                var raw = ConfigurationManager.AppSettings["RT_BasePathStorage"];
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw.Trim();
                }
                return "~/App_Data/AOCR";
            }
        }

        public static bool ValidatePdf(HttpPostedFileBase file, out string error)
        {
            var options = BuildOptions(new[] { ".pdf" }, MaxFileSizeMb, null, true);
            return FileUploadService.TryValidate(file, options, out error);
        }

        public static string SavePdf(HttpPostedFileBase file, string folderRelative)
        {
            string error;
            FileUploadResult result;
            var options = BuildOptions(new[] { ".pdf" }, MaxFileSizeMb, folderRelative, true);
            if (!FileUploadService.TrySave(file, options, out result, out error))
            {
                throw new InvalidOperationException(error ?? "No se pudo guardar el archivo.");
            }

            return BuildReturnPath(folderRelative, result.StoredName);
        }

        // Generic file validation for comprobantes (pdf/jpg/png). Reusable across controllers.
        public static bool ValidateFile(HttpPostedFileBase file, out string error, string[] allowedExts = null, int? maxSizeMb = null)
        {
            var allowed = allowedExts ?? new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var options = BuildOptions(allowed, maxSizeMb ?? MaxFileSizeMb, null, true);
            return FileUploadService.TryValidate(file, options, out error);
        }

        // Saves file under base path and returns virtual path (e.g. "~/App_Data/AOCR/..")
        public static string SaveFile(HttpPostedFileBase file, string folderRelative)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            string error;
            FileUploadResult result;
            var options = BuildOptions(new[] { ".pdf", ".jpg", ".jpeg", ".png" }, MaxFileSizeMb, folderRelative, true);
            if (!FileUploadService.TrySave(file, options, out result, out error))
            {
                throw new InvalidOperationException(error ?? "No se pudo guardar el archivo.");
            }

            return BuildReturnPath(folderRelative, result.StoredName);
        }

        public static bool DeleteFile(string virtualPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(virtualPath)) return false;
                var path = ResolvePath(virtualPath);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string ComputeSha256(string fullPath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(fullPath))
            {
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string ResolvePath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return storedPath;
            if (storedPath.StartsWith("~"))
            {
                return HttpContext.Current.Server.MapPath(storedPath);
            }
            if (Path.IsPathRooted(storedPath))
            {
                return storedPath;
            }

            var baseDir = GetPhysicalBasePath();
            return Path.Combine(baseDir, storedPath.TrimStart('/', '\\'));
        }

        private static FileUploadOptions BuildOptions(string[] allowedExts, int maxSizeMb, string folderRelative, bool validateMagic)
        {
            return new FileUploadOptions
            {
                BasePath = GetPhysicalBasePath(),
                Subfolder = NormalizeFolder(folderRelative),
                AllowedExtensions = allowedExts,
                AllowedContentTypes = null,
                MaxSizeMb = maxSizeMb,
                ValidateMagicBytes = validateMagic
            };
        }

        private static string NormalizeFolder(string folderRelative)
        {
            if (string.IsNullOrWhiteSpace(folderRelative)) return string.Empty;
            return folderRelative.TrimStart('~', '/', '\\');
        }

        public static string GetPhysicalBasePath(string fallbackVirtualBase)
        {
            var raw = ConfigurationManager.AppSettings["RT_FileStorageRoot"];
            if (!string.IsNullOrWhiteSpace(raw) && Path.IsPathRooted(raw))
            {
                return raw.Trim();
            }

            return HttpContext.Current.Server.MapPath(fallbackVirtualBase);
        }

        private static string GetPhysicalBasePath()
        {
            var raw = ConfigurationManager.AppSettings["RT_FileStorageRoot"];
            if (!string.IsNullOrWhiteSpace(raw) && Path.IsPathRooted(raw))
            {
                return raw.Trim();
            }

            return HttpContext.Current.Server.MapPath(BasePathStorage);
        }

        private static string BuildReturnPath(string folderRelative, string storedName)
        {
            var normalizedFolder = NormalizeFolder(folderRelative);
            if (HasExternalStorageRoot())
            {
                return Path.Combine(normalizedFolder, storedName).Replace("\\", "/");
            }

            if (!string.IsNullOrWhiteSpace(BasePathStorage) && BasePathStorage.StartsWith("~"))
            {
                var relative = Path.Combine(BasePathStorage.TrimEnd('~', '/'), normalizedFolder, storedName).Replace("\\", "/");
                return relative.StartsWith("/") ? "~" + relative : "~" + "/" + relative;
            }

            return Path.Combine(normalizedFolder, storedName).Replace("\\", "/");
        }

        private static bool HasExternalStorageRoot()
        {
            var raw = ConfigurationManager.AppSettings["RT_FileStorageRoot"];
            return !string.IsNullOrWhiteSpace(raw) && Path.IsPathRooted(raw.Trim());
        }
    }
}
