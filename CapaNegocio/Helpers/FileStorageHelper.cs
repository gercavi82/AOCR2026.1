using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Hosting;
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

        public static string FileStorageRoot
        {
            get
            {
                var raw = ConfigurationManager.AppSettings["RT_FileStorageRoot"];
                return !string.IsNullOrWhiteSpace(raw) ? raw.Trim() : null;
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
                var path = ResolvePhysicalPath(virtualPath);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string NormalizeStoredPath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return storedPath;
            }

            var normalized = storedPath.Trim().Replace("\\", "/");

            while (normalized.StartsWith("~/~/", StringComparison.Ordinal))
            {
                normalized = "~/" + normalized.Substring(4);
            }

            while (normalized.StartsWith("~~/", StringComparison.Ordinal))
            {
                normalized = "~/" + normalized.Substring(3);
            }

            while (normalized.StartsWith("//", StringComparison.Ordinal))
            {
                normalized = "/" + normalized.TrimStart('/');
            }

            return normalized;
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

        public static string ResolvePhysicalPath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return storedPath;

            var normalizedPath = NormalizeStoredPath(storedPath);
            if (Path.IsPathRooted(normalizedPath))
            {
                return Path.GetFullPath(normalizedPath);
            }

            var rawRoot = FileStorageRoot;
            if (!string.IsNullOrWhiteSpace(rawRoot) && Path.IsPathRooted(rawRoot))
            {
                var root = rawRoot.Trim();
                string relative = normalizedPath;
                if (relative.StartsWith("~/App_Data/", StringComparison.OrdinalIgnoreCase))
                {
                    relative = relative.Substring("~/App_Data/".Length);
                }
                else if (relative.StartsWith("App_Data/", StringComparison.OrdinalIgnoreCase))
                {
                    relative = relative.Substring("App_Data/".Length);
                }
                else if (relative.StartsWith("~/"))
                {
                    relative = relative.Substring(2);
                }
                else if (relative.StartsWith("/"))
                {
                    relative = relative.Substring(1);
                }

                var candidate = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                // Fallback a almacenamiento local si existe allí históricamente
                var local = MapVirtualPath(normalizedPath);
                if (!string.IsNullOrWhiteSpace(local) && File.Exists(local))
                {
                    return local;
                }

                return candidate;
            }

            return MapVirtualPath(normalizedPath);
        }

        public static string MapVirtualPath(string virtualPath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath)) return virtualPath;
            if (HttpContext.Current != null && virtualPath.StartsWith("~"))
            {
                try { return HttpContext.Current.Server.MapPath(virtualPath); } catch { }
            }
            if (HostingEnvironment.IsHosted && virtualPath.StartsWith("~"))
            {
                try { return HostingEnvironment.MapPath(virtualPath); } catch { }
            }
            var baseDir = GetPhysicalBasePath(BasePathStorage);
            return Path.Combine(baseDir, virtualPath.TrimStart('~', '/', '\\').Replace('/', Path.DirectorySeparatorChar));
        }

        public static IEnumerable<string> GetAllowedStorageRoots()
        {
            var roots = new List<string>();
            var rawRoot = FileStorageRoot;
            if (!string.IsNullOrWhiteSpace(rawRoot) && Path.IsPathRooted(rawRoot))
            {
                roots.Add(Path.GetFullPath(rawRoot.Trim()));
            }

            if (HttpContext.Current != null)
            {
                try { roots.Add(Path.GetFullPath(HttpContext.Current.Server.MapPath("~/App_Data"))); } catch { }
            }
            else if (HostingEnvironment.IsHosted)
            {
                try { roots.Add(Path.GetFullPath(HostingEnvironment.MapPath("~/App_Data"))); } catch { }
            }

            return roots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static string GetPhysicalBasePath(string fallbackVirtualBase)
        {
            var raw = FileStorageRoot;
            if (!string.IsNullOrWhiteSpace(raw) && Path.IsPathRooted(raw))
            {
                var root = raw.Trim();
                if (string.IsNullOrWhiteSpace(fallbackVirtualBase))
                {
                    return root;
                }

                var normalized = fallbackVirtualBase.Replace("\\", "/").Trim();
                string relative = normalized;
                if (relative.StartsWith("~/App_Data/", StringComparison.OrdinalIgnoreCase))
                {
                    relative = relative.Substring("~/App_Data/".Length);
                }
                else if (relative.StartsWith("App_Data/", StringComparison.OrdinalIgnoreCase))
                {
                    relative = relative.Substring("App_Data/".Length);
                }
                else if (relative.StartsWith("~/"))
                {
                    relative = relative.Substring(2);
                }
                else if (relative.StartsWith("/"))
                {
                    relative = relative.Substring(1);
                }

                if (string.IsNullOrWhiteSpace(relative))
                {
                    return root;
                }

                return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            }

            return MapVirtualPath(fallbackVirtualBase);
        }

        private static string GetPhysicalBasePath()
        {
            return GetPhysicalBasePath(BasePathStorage);
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

        private static string BuildReturnPath(string folderRelative, string storedName)
        {
            var normalizedFolder = NormalizeFolder(folderRelative);
            if (!string.IsNullOrWhiteSpace(BasePathStorage) && BasePathStorage.StartsWith("~"))
            {
                var prefix = NormalizeStoredPath(BasePathStorage).TrimEnd('/');
                var relative = string.IsNullOrWhiteSpace(normalizedFolder)
                    ? prefix + "/" + storedName
                    : prefix + "/" + normalizedFolder.Replace("\\", "/").Trim('/') + "/" + storedName;
                return NormalizeStoredPath(relative);
            }

            var fallback = string.IsNullOrWhiteSpace(normalizedFolder)
                ? storedName
                : Path.Combine(normalizedFolder, storedName).Replace("\\", "/");
            return NormalizeStoredPath(fallback);
        }
    }
}
