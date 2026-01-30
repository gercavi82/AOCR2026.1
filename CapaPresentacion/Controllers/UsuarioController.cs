using System;
using System.IO; // Necesario para Path y manejo de archivos
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using CapaUtilidades;
using CapaModelo;
using CapaDatos.DAOs;

namespace CapaPresentacion.Controllers
{
    [AllowAnonymous]
    public class UsuarioController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken] // Esto funcionará porque FormData envía el token automáticamente
        public JsonResult RegistrarUsuario()
        {
            try
            {
                // 1. LEER DATOS DEL FORMULARIO
                // Usamos los 'name' exactos del HTML _ModalRegistroUsuario
                var correo = Request.Form["Correo"];
                var cedula = Request.Form["CedulaIdentificacion"]; // Será el Login
                var nombres = Request.Form["NombreUsuario"];       // Nombres reales
                var apellidos = Request.Form["ApellidoUsuario"];   // Apellidos reales
                var empresaCodigo = Request.Form["Empresa"];

                // Lógica del Rol
                var rolSelect = Request.Form["Rol"];
                var otroRol = Request.Form["OtroRol"];

                // Si eligió "OTRO", usamos lo que escribió en el input de texto
                string rolFinal = (rolSelect != null && rolSelect.ToUpper().Contains("OTRO") && !string.IsNullOrWhiteSpace(otroRol))
                                  ? otroRol.ToUpper()
                                  : rolSelect;

                // 2. VALIDAR EMPRESA (Tu lógica original estaba bien)
                var daoEmpresa = new EmpresaAS400DAO();
                var empresas = daoEmpresa.ObtenerEmpresas();

                if (!empresas.Any(e => e.Codigo == empresaCodigo))
                {
                    return Json(new { success = false, message = "La empresa seleccionada no es válida." });
                }

                // 3. MANEJO DEL ARCHIVO ADJUNTO (PDF)
                string rutaArchivo = null;
                var archivo = Request.Files["ArchivoRepresentante"];

                if (archivo != null && archivo.ContentLength > 0)
                {
                    try
                    {
                        var maxSize = GetMaxUploadSize();
                        var allowed = new[] { ".pdf" };
                        var basePath = GetUploadBasePath("DocumentosLegales");

                        var result = FileUploadService.SaveFile(
                            archivo.InputStream,
                            archivo.FileName,
                            archivo.ContentType,
                            basePath,
                            maxSize,
                            allowed);

                        rutaArchivo = result.StoredPath;
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = "Error al procesar archivo: " + ex.Message });
                    }
                }

                // 4. CREAR OBJETO USUARIO (CORREGIDO)
                Usuario nuevoUsuario = new Usuario
                {
                    NombreUsuario = cedula,              // Login = Cédula
                    Email = correo,
                    // Unimos Nombres y Apellidos para el NombreCompleto
                    NombreCompleto = $"{nombres} {apellidos}".Trim().ToUpper(),
                    Contrasena = "6aed143f116b7cb39338ecdfa1e56e334865c869db4469c35eacf5bdaef2046c", // Hash por defecto
                    Activo = true,
                    Rol = rolFinal // AQUI va el rol, no en el nombre
                    // Nota: Si tu modelo Usuario tiene una propiedad para la ruta del archivo, asígnala aquí:
                    // RutaArchivoLegal = rutaArchivo 
                };

                // 5. GUARDAR EN BASE DE DATOS
                int resultadoId = UsuarioDAO.Crear(nuevoUsuario);

                if (resultadoId > 0)
                {
                    return Json(new { success = true, message = "¡Registro exitoso! Su usuario es su número de cédula." });
                }
                else
                {
                    return Json(new { success = false, message = "No se pudo completar el registro en la base de datos." });
                }
            }
            catch (Exception ex)
            {
                // Loguear el error real en consola o archivo log es recomendable
                return Json(new { success = false, message = "Error en el servidor: " + ex.Message });
            }
        }

        private long GetMaxUploadSize()
        {
            var raw = ConfigurationManager.AppSettings["MaxUploadSize"];
            long size;
            return long.TryParse(raw, out size) ? size : (10 * 1024 * 1024);
        }

        private string GetUploadBasePath(string subfolder)
        {
            var baseSetting = ConfigurationManager.AppSettings["UploadStoragePath"] ?? "~/App_Data/Uploads";
            var basePath = Server.MapPath(baseSetting);
            return Path.Combine(basePath, subfolder ?? string.Empty);
        }
    }
}
