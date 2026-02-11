using System;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Web;

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
            error = null;
            if (file == null || file.ContentLength <= 0)
            {
                error = "Debe adjuntar un archivo PDF.";
                return false;
            }

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (ext != ".pdf")
            {
                error = "El archivo debe ser PDF (.pdf).";
                return false;
            }

            var maxBytes = MaxFileSizeMb * 1024 * 1024;
            if (file.ContentLength > maxBytes)
            {
                error = $"El archivo supera el tamaño máximo permitido ({MaxFileSizeMb}MB).";
                return false;
            }

            return true;
        }

        public static string SavePdf(HttpPostedFileBase file, string folderRelative)
        {
            var baseDir = HttpContext.Current.Server.MapPath(BasePathStorage);
            var folder = Path.Combine(baseDir, folderRelative.TrimStart('~', '/', '\\'));

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var safeName = Path.GetFileName(file.FileName);
            var name = Path.GetFileNameWithoutExtension(safeName);
            var ext = Path.GetExtension(safeName);
            var unique = $"{name}_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";

            var fullPath = Path.Combine(folder, unique);
            file.SaveAs(fullPath);

            var relative = Path.Combine(BasePathStorage.TrimEnd('~', '/'), folderRelative.TrimStart('~', '/'), unique)
                .Replace("\\", "/");

            return relative.StartsWith("/") ? "~" + relative : "~" + "/" + relative;
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
    }
}
