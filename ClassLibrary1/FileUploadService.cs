using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace CapaUtilidades
{
    public class FileUploadResult
    {
        public string OriginalName { get; set; }
        public string StoredName { get; set; }
        public string StoredPath { get; set; }
        public string HashSha256 { get; set; }
        public long SizeBytes { get; set; }
        public string ContentType { get; set; }
    }

    public static class FileUploadService
    {
        public static FileUploadResult SaveFile(Stream inputStream, string originalName, string contentType, string basePath, long maxBytes, string[] allowedExtensions)
        {
            if (inputStream == null) throw new ArgumentNullException("inputStream");
            if (string.IsNullOrWhiteSpace(originalName)) throw new ArgumentException("Nombre de archivo inválido.");
            if (string.IsNullOrWhiteSpace(basePath)) throw new ArgumentException("Ruta base inválida.");

            var safeName = Path.GetFileName(originalName);
            var ext = (Path.GetExtension(safeName) ?? string.Empty).ToLowerInvariant();

            if (allowedExtensions == null || allowedExtensions.Length == 0)
                throw new ArgumentException("No hay extensiones permitidas configuradas.");

            if (!allowedExtensions.Select(e => e.ToLowerInvariant()).Contains(ext))
                throw new ArgumentException("Extensión no permitida: " + ext);

            var storedName = Guid.NewGuid().ToString("N") + ext;
            var folder = Path.GetFullPath(basePath);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fullPath = Path.GetFullPath(Path.Combine(folder, storedName));
            if (!fullPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Ruta de destino inválida.");

            var size = GetStreamLength(inputStream);
            if (size > maxBytes)
                throw new ArgumentException("El archivo excede el tamaño máximo permitido.");

            ValidateMagicBytes(inputStream, ext, contentType);

            SaveStreamToFile(inputStream, fullPath);

            var hash = ComputeSha256(fullPath);

            return new FileUploadResult
            {
                OriginalName = safeName,
                StoredName = storedName,
                StoredPath = fullPath,
                HashSha256 = hash,
                SizeBytes = size,
                ContentType = contentType ?? string.Empty
            };
        }

        private static long GetStreamLength(Stream stream)
        {
            if (stream.CanSeek)
                return stream.Length;

            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                stream.Position = 0;
                return ms.Length;
            }
        }

        private static void ValidateMagicBytes(Stream stream, string extension, string contentType)
        {
            if (!stream.CanSeek)
                return;

            var header = new byte[8];
            var originalPosition = stream.Position;
            stream.Position = 0;
            stream.Read(header, 0, header.Length);
            stream.Position = originalPosition;

            if (extension == ".pdf")
            {
                var sig = System.Text.Encoding.ASCII.GetString(header, 0, 4);
                if (sig != "%PDF")
                    throw new ArgumentException("El archivo no es un PDF válido.");
            }
            else if (extension == ".jpg" || extension == ".jpeg")
            {
                if (!(header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF))
                    throw new ArgumentException("El archivo JPG no es válido.");
            }
            else if (extension == ".png")
            {
                var pngSig = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
                for (int i = 0; i < pngSig.Length; i++)
                {
                    if (header[i] != pngSig[i])
                        throw new ArgumentException("El archivo PNG no es válido.");
                }
            }

            if (!string.IsNullOrWhiteSpace(contentType) && contentType.Length > 200)
                throw new ArgumentException("Content-Type inválido.");
        }

        private static void SaveStreamToFile(Stream inputStream, string fullPath)
        {
            if (inputStream.CanSeek)
                inputStream.Position = 0;

            using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                inputStream.CopyTo(fs);
            }
        }

        private static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                var hash = sha.ComputeHash(fs);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
