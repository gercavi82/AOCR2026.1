using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaModelo;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaUtilidades;

namespace CapaPresentacion.Controllers
{
    [AllowAnonymous]
    public class UsuarioController : Controller
    {
        // =====================================================
        // VALIDACIONES ASÍNCRONAS PARA EL MODAL
        // =====================================================
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ValidarCorreo(string correo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(correo))
                {
                    return Json(new { valido = false, mensaje = "El correo es requerido" });
                }

                var existe = UsuarioDAO.ExisteCorreo(correo);

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
        [ValidateAntiForgeryToken]
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

                var existe = UsuarioDAO.ExisteIdentificacion(identificacion);

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
        [ValidateAntiForgeryToken]
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

                var existe = UsuarioDAO.ExisteRUC(ruc);

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
                var empresaCodigo = Request.Form["EmpresaCodigo"];
                var esRepresentanteValue = (Request.Form["esRepresentanteLegal"] ?? "").Trim();
                var esRepresentante = esRepresentanteValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                      esRepresentanteValue.Equals("on", StringComparison.OrdinalIgnoreCase);
                var aceptaDeclaracionValue = (Request.Form["aceptaDeclaracion"] ?? "").Trim();
                var aceptaDeclaracion = aceptaDeclaracionValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                        aceptaDeclaracionValue.Equals("on", StringComparison.OrdinalIgnoreCase);

                // Identificación según tipo (CI o RUC)
                var identificacionFinal = (tipoIdentificacion == "RUC") ? ruc : identificacion;

                // 2. VALIDAR DATOS REQUERIDOS
                if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(identificacionFinal) ||
                    string.IsNullOrWhiteSpace(nombres) || string.IsNullOrWhiteSpace(apellidos) ||
                    string.IsNullOrWhiteSpace(empresaCodigo))
                {
                    return Json(new { success = false, message = "Todos los campos obligatorios deben completarse" });
                }

                if (!aceptaDeclaracion)
                {
                    return Json(new { success = false, message = "Debe aceptar la declaración de responsabilidad." });
                }

                // Validar unicidad antes de insertar
                if (UsuarioDAO.ExisteCorreo(correo))
                {
                    return Json(new { success = false, message = "Este correo ya está registrado" });
                }

                // Generar código de usuario único (si ya existe, agregar sufijo)
                var codigoUsuarioFinal = GenerarCodigoUsuarioUnico(identificacionFinal);
                if (string.IsNullOrWhiteSpace(codigoUsuarioFinal))
                {
                    return Json(new { success = false, message = "No se pudo generar un código de usuario único. Intente nuevamente." });
                }

                // 3. DOCUMENTO DE DESIGNACIÓN RT (REQUERIDO)
                string rutaDocumento = null;
                var archivoDesignacion = Request.Files["ArchivoDesignacionRT"];
                if (archivoDesignacion == null || archivoDesignacion.ContentLength <= 0)
                {
                    return Json(new { success = false, message = "Debe adjuntar el formulario de designación como RT en PDF." });
                }
                rutaDocumento = GuardarArchivoDesignacionRT(archivoDesignacion, identificacionFinal);
                if (string.IsNullOrEmpty(rutaDocumento))
                {
                    return Json(new { success = false, message = "El formulario de designación debe ser PDF y no superar 2MB." });
                }

                // 4. CREAR USUARIO (con contraseña temporal)
                string passwordTemporal = PasswordHelper.GenerarPasswordAleatoria(10);
                string passwordHash = PasswordHelper.HashPassword(passwordTemporal);

                Usuario nuevoUsuario = new Usuario
                {
                    NombreUsuario = codigoUsuarioFinal,  // Login = Código único
                    CodigoUsuario = codigoUsuarioFinal,
                    Email = correo,
                    NombreCompleto = $"{nombres} {apellidos}".Trim().ToUpper(),
                    Contrasena = passwordHash, // Hash de contraseña temporal
                    Activo = true,
                    Rol = "Solicitante", // Rol por defecto para usuarios externos
                    EmpresaCodigo = empresaCodigo,
                    RutaDocumentoLegal = rutaDocumento
                };

                // 5. GUARDAR USUARIO EN BASE DE DATOS
                int usuarioId = UsuarioDAO.Crear(nuevoUsuario);

                if (usuarioId <= 0)
                {
                    return Json(new { success = false, message = "No se pudo crear el usuario" });
                }

                // Marcar designación RT como pendiente y registrar ruta del documento
                UsuarioDAO.ActualizarDesignacionRT(usuarioId, rutaDocumento);

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
                                string rutaArchivo = GuardarArchivoRepresentante(archivo, identificacionFinal, index);
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

                // 6. Enviar correo con credenciales temporales
                var asunto = "Registro de usuario - Sistema AOCR";
                var cuerpo = $@"
                    <div style='font-family:Arial,sans-serif; font-size:14px; color:#222;'>
                        <p>Estimado/a {nombres} {apellidos},</p>
                        <p>Su cuenta ha sido creada exitosamente.</p>
                        <p><strong>Usuario:</strong> {codigoUsuarioFinal}</p>
                        <p><strong>Contraseña temporal:</strong> {passwordTemporal}</p>
                        <p>Por seguridad, el sistema le pedirá cambiar la contraseña en su primer ingreso.</p>
                        <p>Si usted no solicitó este registro, por favor comuníquese con la DGAC.</p>
                        <hr />
                        <small>Este es un correo automático, por favor no responder.</small>
                    </div>";

                bool correoEnviado = false;
                try
                {
                    var servicioCorreo = new EnviarCorreo();
                    correoEnviado = servicioCorreo.enviaMensajeCorreo(correo, asunto, cuerpo);
                }
                catch
                {
                    correoEnviado = false;
                }

                var mensajeFinal = "Usuario registrado exitosamente. Su nombre de usuario es: " + codigoUsuarioFinal;
                if (!correoEnviado)
                {
                    mensajeFinal += ". No se pudo enviar el correo. Verifique configuración SMTP.";
                }

                return Json(new
                {
                    success = true,
                    message = mensajeFinal,
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

                var options = new FileUploadOptions
                {
                    BasePath = FileStorageHelper.GetPhysicalBasePath("~/App_Data/DocumentosLegales"),
                    Subfolder = string.Empty,
                    AllowedExtensions = new[] { ".pdf" },
                    AllowedContentTypes = new[] { "application/pdf" },
                    MaxSizeMb = 2,
                    ValidateMagicBytes = true
                };

                string error;
                FileUploadResult result;
                if (!FileUploadService.TrySave(archivo, options, out result, out error))
                {
                    return null;
                }

                return "~/App_Data/DocumentosLegales/" + result.StoredName;
            }
            catch
            {
                return null;
            }
        }

        private string GuardarArchivoDesignacionRT(HttpPostedFileBase archivo, string identificacion)
        {
            try
            {
                var ext = Path.GetExtension(archivo.FileName).ToLower();
                if (ext != ".pdf")
                {
                    return null;
                }

                var options = new FileUploadOptions
                {
                    BasePath = FileStorageHelper.GetPhysicalBasePath("~/App_Data/DesignacionesRT"),
                    Subfolder = string.Empty,
                    AllowedExtensions = new[] { ".pdf" },
                    AllowedContentTypes = new[] { "application/pdf" },
                    MaxSizeMb = 2,
                    ValidateMagicBytes = true
                };

                string error;
                FileUploadResult result;
                if (!FileUploadService.TrySave(archivo, options, out result, out error))
                {
                    return null;
                }

                return "~/App_Data/DesignacionesRT/" + result.StoredName;
            }
            catch
            {
                return null;
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult DescargarFormularioDesignacionRT()
        {
            var contenido = @"FORMULARIO DE DESIGNACIÓN COMO RT

Yo, ______________________________, Director de Operaciones de la compañía ______________________________,
designo al Sr./Sra. ______________________________ como Responsable Técnico (RT) para las estaciones regulares
de Ecuador, comprometiéndome a mantener la coordinación necesaria con la DGAC.

Firma Director de Operaciones: ______________________________
Fecha: ____/____/________
";

            var bytes = System.Text.Encoding.UTF8.GetBytes(contenido);
            return File(bytes, "text/plain", "Formulario_Designacion_RT.txt");
        }

        // =====================================================
        // CÓDIGO ÚNICO DE USUARIO
        // =====================================================
        private string GenerarCodigoUsuarioUnico(string baseCodigo)
        {
            if (string.IsNullOrWhiteSpace(baseCodigo))
                return null;

            var candidato = baseCodigo.Trim();
            if (!UsuarioDAO.ExisteIdentificacion(candidato))
                return candidato;

            // Intentar con sufijo incremental
            for (int i = 1; i <= 999; i++)
            {
                var sufijo = i.ToString("D3");
                var alterno = $"{baseCodigo}-{sufijo}";
                if (!UsuarioDAO.ExisteIdentificacion(alterno))
                    return alterno;
            }

            return null;
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

                    var options = new FileUploadOptions
                    {
                        BasePath = FileStorageHelper.GetPhysicalBasePath("~/App_Data/DocumentosLegales"),
                        Subfolder = string.Empty,
                        AllowedExtensions = new[] { ".pdf" },
                        AllowedContentTypes = new[] { "application/pdf" },
                        MaxSizeMb = 2,
                        ValidateMagicBytes = true
                    };

                    string error;
                    FileUploadResult result;
                    if (!FileUploadService.TrySave(archivo, options, out result, out error))
                    {
                        return Json(new { success = false, message = error ?? "No se pudo guardar el archivo." });
                    }

                    rutaArchivo = Path.Combine("~/App_Data/DocumentosLegales/", result.StoredName);
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

        // =====================================================
        // REVISIÓN DE DESIGNACIONES RT POR COORDINADOR
        // =====================================================
        [HttpGet]
        [Authorize(Roles = "Coordinador")] // Ajusta el rol según tu sistema
        public ActionResult RevisarDesignaciones()
        {
            // Filtrar usuarios con documento de designación pendiente de revisión
            var usuarios = UsuarioDAO.ObtenerUsuariosPendientesDesignacion(); // Debes implementar este método en tu DAO
            return View("RevisarDesignaciones", usuarios);
        }

        [HttpGet]
        [Authorize(Roles = "Coordinador")]
        public ActionResult DescargarDesignacionRT(int id)
        {
            var usuario = UsuarioDAO.ObtenerPorId(id);
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.RutaDocumentoLegal))
            {
                return HttpNotFound();
            }

            var rutaFisica = Server.MapPath(usuario.RutaDocumentoLegal);
            if (!System.IO.File.Exists(rutaFisica))
            {
                return HttpNotFound();
            }

            return File(rutaFisica, "application/pdf", $"DesignacionRT_{usuario.CodigoUsuario}.pdf");
        }

        [HttpGet]
        [Authorize(Roles = "Coordinador")]
        public ActionResult DescargarConstanciaRT(int id)
        {
            var usuario = UsuarioDAO.ObtenerPorId(id);
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.RutaConstanciaRT))
            {
                return HttpNotFound();
            }

            var rutaFisica = Server.MapPath(usuario.RutaConstanciaRT);
            if (!System.IO.File.Exists(rutaFisica))
            {
                return HttpNotFound();
            }

            return File(rutaFisica, "text/plain", $"ConstanciaRT_{usuario.CodigoUsuario}.txt");
        }

        [HttpPost]
        [Authorize(Roles = "Coordinador")]
        [ValidateAntiForgeryToken]
        public ActionResult AceptarDesignacion(int id)
        {
            // Lógica para marcar como aceptado y generar constancia
            var usuario = UsuarioDAO.ObtenerPorId(id);
            if (usuario == null)
            {
                TempData["error"] = "Usuario no encontrado.";
                return RedirectToAction("RevisarDesignaciones");
            }
            // Generar constancia y obtener la ruta
            string rutaConstancia = GenerarConstanciaRT(usuario);
            // Marcar como aceptado y guardar la ruta de la constancia
            UsuarioDAO.AceptarDesignacionRT(id, rutaConstancia);
            TempData["msg"] = "Designación aceptada y constancia generada.";
            return RedirectToAction("RevisarDesignaciones");
        }

        [HttpPost]
        [Authorize(Roles = "Coordinador")]
        [ValidateAntiForgeryToken]
        public ActionResult RechazarDesignacion(int id)
        {
            // Lógica para marcar como rechazada
            var usuario = UsuarioDAO.ObtenerPorId(id);
            if (usuario == null)
            {
                TempData["error"] = "Usuario no encontrado.";
                return RedirectToAction("RevisarDesignaciones");
            }
            UsuarioDAO.RechazarDesignacionRT(id);
            TempData["msg"] = "Designación rechazada.";
            return RedirectToAction("RevisarDesignaciones");
        }

        // Simulación de generación de constancia (puedes reemplazar por PDF real)
        private string GenerarConstanciaRT(Usuario usuario)
        {
            string carpeta = Server.MapPath("~/App_Data/ConstanciasRT/");
            if (!Directory.Exists(carpeta)) Directory.CreateDirectory(carpeta);
            string nombreArchivo = $"Constancia_{usuario.CodigoUsuario}_{DateTime.Now:yyyyMMddHHmmss}.txt";
            string archivo = Path.Combine(carpeta, nombreArchivo);
            System.IO.File.WriteAllText(archivo, $"Constancia de aceptación de designación RT para {usuario.NombreCompleto} ({usuario.CodigoUsuario}) - Fecha: {DateTime.Now}");
            // Retornar la ruta relativa para guardar en la BD
            return $"~/App_Data/ConstanciasRT/{nombreArchivo}";
        }
    }
}
