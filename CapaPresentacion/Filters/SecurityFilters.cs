using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;
using CapaUtilidades;
using System.Web.Routing;

namespace CapaPresentacion.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class NoCacheAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            var response = filterContext.HttpContext.Response;

            response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            response.Cache.SetValidUntilExpires(false);
            response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Cache.SetNoStore();

            response.AppendHeader("Pragma", "no-cache");
            response.AppendHeader("Expires", "0");

            base.OnResultExecuting(filterContext);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ValidateModelAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var controller = filterContext.Controller;
            var modelState = controller?.ViewData?.ModelState;

            if (modelState != null && !modelState.IsValid)
            {
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    var errors = modelState
                        .Where(kvp => kvp.Value != null && kvp.Value.Errors != null && kvp.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.Select(e =>
                                string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Error de validación." : e.ErrorMessage
                            ).ToArray()
                        );

                    filterContext.Result = new JsonResult
                    {
                        Data = new { success = false, errors },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                    return;
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class AuthorizeRolesAttribute : AuthorizeAttribute
    {
        public AuthorizeRolesAttribute(params string[] roles)
        {
            Roles = string.Join(",", roles ?? new string[0]);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.Request.IsAuthenticated)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary {
                        { "controller", "Error" },
                        { "action", "AccesoDenegado" }
                    });
            }
            else
            {
                base.HandleUnauthorizedRequest(filterContext);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class SanitizeInputAttribute : FilterAttribute, IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext filterContext) { }
        public void OnActionExecuted(ActionExecutedContext filterContext) { }
    }

    /// <summary>
    /// Filtro global para validación de seguridad
    /// </summary>
    public class GlobalSecurityFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Validar ModelState automáticamente para POST
            if (filterContext.HttpContext.Request.HttpMethod == "POST")
            {
                if (!filterContext.Controller.ViewData.ModelState.IsValid)
                {
                    // Log de modelo inválido
                    System.Diagnostics.Debug.WriteLine(
                        "ModelState inválido en: " + filterContext.ActionDescriptor.ActionName);
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }

    /// <summary>
    /// Validador de archivos subidos
    /// </summary>
    public static class FileUploadValidator
    {
        // Extensiones permitidas
        private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".gif" };
        
        // Tamaño máximo: 5MB
        private const long MaxFileSize = 5 * 1024 * 1024;

        // Magic bytes para validación de tipo real
        private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };
        private static readonly byte[] GifMagic = { 0x47, 0x49, 0x46 }; // GIF

        /// <summary>
        /// Resultado de validación de archivo
        /// </summary>
        public class FileValidationResult
        {
            public bool IsValid { get; set; }
            public string Error { get; set; }
            public string SafeFileName { get; set; }
            public string FileHash { get; set; }
            public string DetectedType { get; set; }

            public static FileValidationResult Success(string safeFileName, string hash, string type)
            {
                return new FileValidationResult
                {
                    IsValid = true,
                    SafeFileName = safeFileName,
                    FileHash = hash,
                    DetectedType = type
                };
            }

            public static FileValidationResult Fail(string error)
            {
                return new FileValidationResult { IsValid = false, Error = error };
            }
        }

        /// <summary>
        /// Valida un archivo subido de forma segura
        /// </summary>
        public static FileValidationResult ValidateFile(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
            {
                return FileValidationResult.Fail("No se proporcionó ningún archivo.");
            }

            // 1. Validar tamaño
            if (file.ContentLength > MaxFileSize)
            {
                return FileValidationResult.Fail(
                    string.Format("El archivo excede el tamaño máximo permitido de {0}MB.",
                        MaxFileSize / 1024 / 1024));
            }

            // 2. Obtener nombre seguro (prevenir path traversal)
            var originalName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(originalName))
            {
                return FileValidationResult.Fail("Nombre de archivo inválido.");
            }

            // 3. Validar extensión
            var extension = Path.GetExtension(originalName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                return FileValidationResult.Fail(
                    string.Format("Extensión no permitida. Permitidas: {0}",
                        string.Join(", ", AllowedExtensions)));
            }

            // 4. Leer bytes para validar magic bytes y calcular hash
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                file.InputStream.Position = 0;
                file.InputStream.CopyTo(ms);
                fileBytes = ms.ToArray();
                file.InputStream.Position = 0; // Reset para uso posterior
            }

            // 5. Validar magic bytes
            var detectedType = DetectFileType(fileBytes);
            if (string.IsNullOrEmpty(detectedType))
            {
                return FileValidationResult.Fail("Tipo de archivo no reconocido o potencialmente malicioso.");
            }

            // 6. Verificar que extensión coincide con tipo detectado
            if (!ExtensionMatchesType(extension, detectedType))
            {
                return FileValidationResult.Fail(
                    string.Format("La extensión ({0}) no coincide con el tipo real del archivo ({1}).",
                        extension, detectedType));
            }

            // 7. Generar nombre seguro con GUID
            var safeFileName = string.Format("{0}_{1}{2}",
                Guid.NewGuid().ToString("N"),
                DateTime.UtcNow.Ticks,
                extension);

            // 8. Calcular hash SHA256
            string fileHash;
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(fileBytes);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            return FileValidationResult.Success(safeFileName, fileHash, detectedType);
        }

        /// <summary>
        /// Detecta el tipo real del archivo por magic bytes
        /// </summary>
        private static string DetectFileType(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length < 4)
            {
                return null;
            }

            if (StartsWithBytes(fileBytes, PdfMagic))
            {
                return "application/pdf";
            }

            if (StartsWithBytes(fileBytes, JpegMagic))
            {
                return "image/jpeg";
            }

            if (StartsWithBytes(fileBytes, PngMagic))
            {
                return "image/png";
            }

            if (StartsWithBytes(fileBytes, GifMagic))
            {
                return "image/gif";
            }

            return null;
        }

        /// <summary>
        /// Verifica si el archivo comienza con los bytes especificados
        /// </summary>
        private static bool StartsWithBytes(byte[] fileBytes, byte[] magicBytes)
        {
            if (fileBytes.Length < magicBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < magicBytes.Length; i++)
            {
                if (fileBytes[i] != magicBytes[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Verifica que la extensión coincide con el tipo detectado
        /// </summary>
        private static bool ExtensionMatchesType(string extension, string detectedType)
        {
            switch (detectedType)
            {
                case "application/pdf":
                    return extension == ".pdf";
                case "image/jpeg":
                    return extension == ".jpg" || extension == ".jpeg";
                case "image/png":
                    return extension == ".png";
                case "image/gif":
                    return extension == ".gif";
                default:
                    return false;
            }
        }

        /// <summary>
        /// Guarda archivo de forma segura fuera del webroot
        /// </summary>
        public static string SaveFileSecurely(HttpPostedFileBase file, FileValidationResult validation, string baseDirectory)
        {
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("No se puede guardar un archivo inválido.");
            }

            var fullDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, baseDirectory);
            var ext = Path.GetExtension(validation.SafeFileName);

            var options = new FileUploadOptions
            {
                BasePath = fullDirectory,
                Subfolder = string.Empty,
                AllowedExtensions = string.IsNullOrWhiteSpace(ext) ? null : new[] { ext.ToLowerInvariant() },
                AllowedContentTypes = null,
                MaxSizeMb = 0,
                ValidateMagicBytes = false
            };

            string error;
            FileUploadResult result;
            if (!FileUploadService.TrySave(file, options, out result, out error))
            {
                throw new InvalidOperationException(error ?? "No se pudo guardar el archivo.");
            }

            return Path.Combine(baseDirectory, result.StoredName);
        }
    }

    /// <summary>
    /// Excepción de seguridad personalizada
    /// </summary>
    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
    }
}
