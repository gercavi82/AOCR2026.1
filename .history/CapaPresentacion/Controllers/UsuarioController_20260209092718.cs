using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaModelo;
using CapaDatos.DAOs;
using CapaNegocio;

namespace CapaPresentacion.Controllers
{
    [AllowAnonymous]
    public class UsuarioController : Controller
    {
        private readonly UsuarioDAO _usuarioDAO;

        public UsuarioController()
        {
            _usuarioDAO = new UsuarioDAO();
        }

        // =====================================================
        // VALIDACIONES ASÍNCRONAS PARA EL MODAL
        // =====================================================
        
        [HttpPost]
        public JsonResult ValidarCorreo(string correo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(correo))
                {
                    return Json(new { valido = false, mensaje = "El correo es requerido" });
                }

                var existe = _usuarioDAO.ExisteCorreo(correo);

                if (existe)
                {
                    return Json(new { valido = false, mensaje = "Este correo ya está registrado" });
                }

                return Json(new { valido = true });
            }
            catch (Exception ex)
            {
                return Json(new { valido = false, mensaje = "Error al validar: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult ValidarIdentificacion(string identificacion, string tipo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(identificacion))
                {
                    return Json(new { valido = false, mensaje = "La identificación es requerida" });
                }

                // Validar formato según tipo
                if (tipo == "CI" && identificacion.Length != 10)
                {
                    return Json(new { valido = false, mensaje = "La cédula debe tener 10 dígitos" });
                }

                var existe = _usuarioDAO.ExisteIdentificacion(identificacion);

                if (existe)
                {
                    return Json(new { valido = false, mensaje = "Esta identificación ya está registrada" });
                }

                return Json(new { valido = true });
            }
            catch (Exception ex)
            {
                return Json(new { valido = false, mensaje = "Error al validar: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult ValidarRUC(string ruc)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ruc))
                {
                    return Json(new { valido = false, mensaje = "El RUC es requerido" });
                }

                if (ruc.Length != 13)
                {
                    return Json(new { valido = false, mensaje = "El RUC debe tener 13 dígitos" });
                }

                var existe = _usuarioDAO.ExisteRUC(ruc);

                if (existe)
                {
                    return Json(new { valido = false, mensaje = "Este RUC ya está registrado" });
                }

                return Json(new { valido = true });
            }
            catch (Exception ex)
            {
                return Json(new { valido = false, mensaje = "Error al validar: " + ex.Message });
            }
        }

        // =====================================================
        // CREAR USUARIO CON MÚLTIPLES COMPAÑÍAS
        // =====================================================
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Crear()
        {
            try
            {
                // 1. LEER DATOS BÁSICOS
                var correo = Request.Form["Correo"];
                var tipoIdentificacion = Request.Form["TipoIdentificacion"];
                var identificacion = Request.Form["CedulaIdentificacion"];
                var ruc = Request.Form["RUC"];
                var nombres = Request.Form["NombreUsuario"];
                var apellidos = Request.Form["ApellidoUsuario"];
                var esRepresentante = Request.Form["esRepresentanteLegal"] == "true";

                // 2. VALIDAR DATOS REQUERIDOS
                if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(identificacion) ||
                    string.IsNullOrWhiteSpace(ruc) || string.IsNullOrWhiteSpace(nombres) || 
                    string.IsNullOrWhiteSpace(apellidos))
                {
                    return Json(new { success = false, message = "Todos los campos obligatorios deben completarse" });
                }

                // 3. CREAR USUARIO
                Usuario nuevoUsuario = new Usuario
                {
                    NombreUsuario = identificacion,  // Login = Identificación
                    Email = correo,
                    NombreCompleto = $"{nombres} {apellidos}".Trim().ToUpper(),
                    Contrasena = "6aed143f116b7cb39338ecdfa1e56e334865c869db4469c35eacf5bdaef2046c", // Hash por defecto (cambiar en primer login)
                    Activo = true,
                    Rol = "Solicitante" // Rol por defecto para usuarios externos
                };

                // 4. GUARDAR USUARIO EN BASE DE DATOS
                int usuarioId = _usuarioDAO.Crear(nuevoUsuario);

                if (usuarioId <= 0)
                {
                    return Json(new { success = false, message = "No se pudo crear el usuario" });
                }

                // 5. SI ES REPRESENTANTE LEGAL, PROCESAR COMPAÑÍAS Y ARCHIVOS
                if (esRepresentante)
                {
                    var companias = new List<int>();
                    var archivos = new List<string>();

                    // Buscar todos los índices de compañías
                    int index = 0;
                    while (Request.Form[$"Companias[{index}].IdCompania"] != null)
                    {
                        var idCompaniaStr = Request.Form[$"Companias[{index}].IdCompania"];
                        
                        if (!string.IsNullOrWhiteSpace(idCompaniaStr) && int.TryParse(idCompaniaStr, out int idCompania))
                        {
                            companias.Add(idCompania);

                            // Procesar archivo asociado
                            var archivo = Request.Files[$"Companias[{index}].ArchivoRepresentante"];
                            if (archivo != null && archivo.ContentLength > 0)
                            {
                                string rutaArchivo = GuardarArchivoRepresentante(archivo, identificacion, index);
                                if (!string.IsNullOrEmpty(rutaArchivo))
                                {
                                    archivos.Add(rutaArchivo);
                                    
                                    // TODO: Guardar relación usuario-compañía-archivo en tabla correspondiente
                                    // RepresentanteLegalBL.CrearRelacion(usuarioId, idCompania, rutaArchivo);
                                }
                            }
                        }

                        index++;
                    }

                    if (companias.Count == 0)
                    {
                        return Json(new { success = false, message = "Debe seleccionar al menos una compañía" });
                    }
                }

                return Json(new 
                { 
                    success = true, 
                    message = "Usuario registrado exitosamente. Su nombre de usuario es su identificación.",
                    usuarioId = usuarioId
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error en el servidor: " + ex.Message });
            }
        }

        // =====================================================
        // MÉTODO AUXILIAR PARA GUARDAR ARCHIVOS
        // =====================================================
        
        private string GuardarArchivoRepresentante(HttpPostedFileBase archivo, string identificacion, int index)
        {
            try
            {
                // Validar extensión
                var ext = Path.GetExtension(archivo.FileName).ToLower();
                if (ext != ".pdf")
                {
                    return null;
                }

                // Validar tamaño (2MB máximo)
                if (archivo.ContentLength > 2 * 1024 * 1024)
                {
                    return null;
                }

                // Crear carpeta si no existe
                string carpetaDestino = Server.MapPath("~/App_Data/DocumentosLegales/");
                if (!Directory.Exists(carpetaDestino))
                {
                    Directory.CreateDirectory(carpetaDestino);
                }

                // Nombre único: IDENTIFICACION_INDEX_TIMESTAMP.pdf
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string nombreArchivo = $"{identificacion}_{index}_{timestamp}.pdf";
                string rutaCompleta = Path.Combine(carpetaDestino, nombreArchivo);

                // Guardar archivo
                archivo.SaveAs(rutaCompleta);

                // Retornar ruta relativa para guardar en BD
                return $"~/App_Data/DocumentosLegales/{nombreArchivo}";
            }
            catch
            {
                return null;
            }
        }

        // =====================================================
        // MÉTODO LEGACY (MANTENER SI SE USA EN OTRA PARTE)
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                    // Validación simple de extensión
                    var ext = Path.GetExtension(archivo.FileName).ToLower();
                    if (ext != ".pdf")
                    {
                        return Json(new { success = false, message = "Solo se permiten archivos PDF." });
                    }

                    // Definir ruta de guardado (Ej: /App_Data/DocumentosLegales/)
                    // Es más seguro guardar en App_Data para que no sea accesible públicamente por URL directa
                    string carpetaDestino = Server.MapPath("~/App_Data/DocumentosLegales/");

                    if (!Directory.Exists(carpetaDestino))
                        Directory.CreateDirectory(carpetaDestino);

                    // Nombre único para evitar reemplazar archivos: CEDULA_NombreOriginal.pdf
                    string nombreArchivo = $"{cedula}_{Path.GetFileName(archivo.FileName)}";
                    rutaArchivo = Path.Combine(carpetaDestino, nombreArchivo);

                    // Guardar en disco
                    archivo.SaveAs(rutaArchivo);
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
    }
}