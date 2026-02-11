using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;

namespace CapaUtilidades
{
    public class FileUploadOptions
    {
        public string BasePath { get; set; }
        public string Subfolder { get; set; }
        public int MaxSizeMb { get; set; }
        public string[] AllowedExtensions { get; set; }
        public string[] AllowedContentTypes { get; set; }
        public bool ValidateMagicBytes { get; set; }
    }

    public class FileUploadResult
    {
        public string OriginalName { get; set; }
        public string StoredName { get; set; }
        public string StoredPath { get; set; }
        public string RelativePath { get; set; }
        public string HashSha256 { get; set; }
        public long Size { get; set; }
        public string ContentType { get; set; }
        public string Extension { get; set; }
    }

    public static class FileUploadService
    {
        public static bool TryValidate(HttpPostedFileBase file, FileUploadOptions options, out string error)
        {
            error = null;
            if (file == null || file.ContentLength <= 0)
            {
                error = "Debe adjuntar un archivo.";
                return false;
            }

            if (options == null)
            {
                error = "Opciones de carga inválidas.";
                return false;
            }

            var ext = (Path.GetExtension(file.FileName) ?? string.Empty).ToLowerInvariant();
            var allowedExts = options.AllowedExtensions ?? new string[0];
            if (allowedExts.Length > 0 && !allowedExts.Contains(ext))
            {
                error = "Formato no permitido.";
                return false;
            }

            var maxBytes = options.MaxSizeMb > 0 ? (long)options.MaxSizeMb * 1024 * 1024 : 0;
            if (maxBytes > 0 && file.ContentLength > maxBytes)
            {
                error = "El archivo supera el tamaño máximo permitido.";
                return false;
            }

            if (options.AllowedContentTypes != null && options.AllowedContentTypes.Length > 0)
            {
                var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
                if (!options.AllowedContentTypes.Any(t => t.Equals(contentType, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "Tipo de contenido no permitido.";
                    return false;
                }
            }

            if (options.ValidateMagicBytes && !ValidateMagicBytes(file, ext))
            {
                error = "El archivo no coincide con el tipo esperado.";
                return false;
            }

            return true;
        }

        public static bool TrySave(HttpPostedFileBase file, FileUploadOptions options, out FileUploadResult result, out string error)
        {
            result = null;
            if (!TryValidate(file, options, out error))
            {
                return false;
            }

            var basePath = options.BasePath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(basePath))
            {
                error = "Ruta base no configurada.";
                return false;
            }

            var subfolder = options.Subfolder ?? string.Empty;
            if (IsInvalidSubfolder(subfolder))
            {
                error = "Ruta de almacenamiento inválida.";
                return false;
            }

            var safeOriginal = Path.GetFileName(file.FileName);
            var ext = (Path.GetExtension(safeOriginal) ?? string.Empty).ToLowerInvariant();
            var storedName = Guid.NewGuid().ToString("N") + ext;

            var targetDir = string.IsNullOrWhiteSpace(subfolder)
                ? basePath
                : Path.Combine(basePath, subfolder);

            var normalizedBase = Path.GetFullPath(basePath);
            var normalizedTarget = Path.GetFullPath(targetDir);
            if (!normalizedTarget.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            {
                error = "Ruta de almacenamiento inválida.";
                return false;
            }

            if (!Directory.Exists(normalizedTarget))
            {
                Directory.CreateDirectory(normalizedTarget);
            }

            var fullPath = Path.Combine(normalizedTarget, storedName);
            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.InputStream.Position = 0;
                file.InputStream.CopyTo(stream);
            }

            var hash = ComputeSha256(fullPath);

            result = new FileUploadResult
            {
                OriginalName = safeOriginal,
                StoredName = storedName,
                StoredPath = fullPath,
                RelativePath = string.IsNullOrWhiteSpace(subfolder) ? storedName : Path.Combine(subfolder, storedName),
                HashSha256 = hash,
                Size = file.ContentLength,
                ContentType = file.ContentType,
                Extension = ext
            };

            return true;
        }

        private static bool IsInvalidSubfolder(string subfolder)
        {
            if (string.IsNullOrWhiteSpace(subfolder)) return false;
            if (Path.IsPathRooted(subfolder)) return true;
            if (subfolder.Contains("..")) return true;
            return false;
        }

        private static bool ValidateMagicBytes(HttpPostedFileBase file, string ext)
        {
            if (file == null) return false;
            if (!file.InputStream.CanSeek) return true; // no validar si no se puede leer

            var buffer = new byte[8];
            var originalPos = file.InputStream.Position;
            try
            {
                file.InputStream.Position = 0;
                var read = file.InputStream.Read(buffer, 0, buffer.Length);
                if (read <= 0) return false;

                if (ext == ".pdf")
                {
                    return buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46; // %PDF
                }

                if (ext == ".png")
                {
                    return buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
                           buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A;
                }

                if (ext == ".jpg" || ext == ".jpeg")
                {
                    return buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF;
                }

                return true;
            }
            finally
            {
                file.InputStream.Position = originalPos;
            }
        }

        private static string ComputeSha256(string fullPath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(fullPath))
            {
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
