using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using CapaModelo;
using CapaModelo.RT.ViewModels;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaNegocio.Services;
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
                    var tmpDao = new DeclaracionTemporalDAO();
                    var tmp = tmpDao.GetByEmail((correo ?? string.Empty).Trim().ToLower());
                    if (tmp != null && tmp.Aceptada)
                    {
                        aceptaDeclaracion = true;
                    }
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

                // Generar código de usuario único (primera letra nombre + segunda letra segundo nombre + apellido)
                var codigoUsuarioFinal = GenerarCodigoUsuarioUnico(nombres, apellidos);
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

                // 4.1 SINCRONIZAR A AS400 (si está habilitado)
                if (SyncUsuariosAs400Enabled())
                {
                    var usuarioAs400 = UsuarioAs400Record.CrearBasico(
                        codigoUsuarioFinal,
                        nombres?.Trim(),
                        apellidos?.Trim(),
                        tipoIdentificacion,
                        identificacionFinal,
                        correo?.Trim(),
                        passwordHash,
                        "AOCR");

                    if (string.Equals(tipoIdentificacion, "RUC", StringComparison.OrdinalIgnoreCase))
                    {
                        usuarioAs400.TipoTributario = "RUC";
                        usuarioAs400.NumeroRuc = ruc?.Trim();
                    }

                    string as400Error;
                    var as400Dao = new UsuarioAS400DAO();
                    if (!as400Dao.UpsertUsuarioCompleto(usuarioAs400, out as400Error))
                    {
                        return Json(new { success = false, message = "Error al registrar usuario en AS400: " + as400Error });
                    }
                }

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

                // 5.1 Guardar aceptación de declaración en BD (RT) y notificar por correo
                bool declaracionRegistrada = false;
                try
                {
                    var daoEmpresa = new EmpresaAS400DAO();
                    var empresa = daoEmpresa.ObtenerEmpresaPorCodigo(empresaCodigo);
                    var nombreEmpresa = empresa != null && !string.IsNullOrWhiteSpace(empresa.Nombre)
                        ? empresa.Nombre
                        : empresaCodigo;

                    var rtService = new RTService();
                    var solicitudExistente = rtService.GetSolicitudByUsuario(usuarioId);
                    int solicitudId;

                    if (solicitudExistente == null)
                    {
                        var registroVm = new RegistroRTVM
                        {
                            RazonSocial = nombreEmpresa,
                            Ruc = (ruc ?? string.Empty).Trim(),
                            Telefono = string.Empty,
                            Email = correo,
                            AreaContableJson = null
                        };

                        solicitudId = rtService.GuardarBorrador(registroVm, usuarioId);
                    }
                    else
                    {
                        solicitudId = solicitudExistente.Id;
                    }

                    rtService.AceptarDeclaracion(solicitudId, usuarioId);
                    declaracionRegistrada = true;

                    try
                    {
                        var asuntoDecl = "Declaración de responsabilidad aceptada - Sistema AOCR";
                        var cuerpoDecl = $@"
                            <div style='font-family:Arial,sans-serif; font-size:14px; color:#222;'>
                                <p>Estimado/a {nombres} {apellidos},</p>
                                <p>Hemos registrado la <strong>aceptación</strong> de su declaración de responsabilidad RT.</p>
                                <p><strong>Empresa:</strong> {nombreEmpresa}</p>
                                <p>Su solicitud queda en proceso de validación por la DGAC.</p>
                                <hr />
                                <small>Este es un correo automático, por favor no responder.</small>
                            </div>";

                        var servicioCorreoDecl = new EnviarCorreo();
                        servicioCorreoDecl.enviaMensajeCorreo(correo, asuntoDecl, cuerpoDecl);
                    }
                    catch
                    {
                        // No bloquear el flujo si falla el correo de declaración
                    }
                }
                catch
                {
                    declaracionRegistrada = false;
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

                // 6. Enviar correo informativo: la cuenta fue creada, las credenciales llegarán al aprobar RT
                var asunto = "Cuenta creada - Sistema AOCR (pendiente de aprobación)";
                var cuerpo = $@"
                    <div style='font-family:Arial,sans-serif; font-size:14px; color:#222;'>
                        <p>Estimado/a {nombres} {apellidos},</p>
                        <p>Su cuenta en el <strong>Sistema AOCR</strong> ha sido creada exitosamente.</p>
                        <p>Su solicitud de designación como <strong>Responsable Técnico (RT)</strong> se encuentra
                           en proceso de revisión y aprobación por la DGAC.</p>
                        <p>Una vez que su designación sea <strong>aprobada</strong>, recibirá un correo
                           con su <strong>usuario y contraseña</strong> para acceder al sistema.</p>
                        <p>Si usted no solicitó este registro, por favor comuníquese con la DGAC de inmediato.</p>
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

                var mensajeFinal = "Usuario registrado exitosamente. Recibirá sus credenciales de acceso una vez que su designación RT sea aprobada.";
                if (!correoEnviado)
                {
                    mensajeFinal += " No se pudo enviar el correo de confirmación. Verifique configuración SMTP.";
                }
                if (!declaracionRegistrada)
                {
                    mensajeFinal += " No se pudo registrar la aceptación de la declaración en este momento.";
                }

                try
                {
                    var tmpDao = new DeclaracionTemporalDAO();
                    tmpDao.DeleteByEmail((correo ?? string.Empty).Trim().ToLower());
                }
                catch
                {
                    // no bloquear por limpieza temporal
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
            using (var ms = new MemoryStream())
            {
                var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 25f, 25f, 120f, 80f);
                var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);
                var server = System.Web.HttpContext.Current != null ? System.Web.HttpContext.Current.Server : null;
                writer.PageEvent = PdfBrandingHelper.CreateITextPageEvent(server, "UsuarioController.DescargarFormularioDesignacionRT");
                doc.Open();

                var titleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14);
                var subtitleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 10);
                var normalFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 11);
                var labelFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 10);

                var titulo = new iTextSharp.text.Paragraph("FORMULARIO DE DESIGNACIÓN COMO RT", titleFont);
                titulo.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                titulo.SpacingAfter = 6f;
                doc.Add(titulo);

                var subtitulo = new iTextSharp.text.Paragraph("Dirección General de Aviación Civil", subtitleFont);
                subtitulo.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                subtitulo.SpacingAfter = 14f;
                doc.Add(subtitulo);

                var line = new iTextSharp.text.pdf.draw.LineSeparator(0.5f, 100f, new iTextSharp.text.BaseColor(120, 120, 120), iTextSharp.text.Element.ALIGN_CENTER, -2f);
                doc.Add(line);
                doc.Add(new iTextSharp.text.Paragraph(" "));

                var linea1 = new iTextSharp.text.Paragraph("Yo, ______________________________, Director de Operaciones de la compañía", normalFont);
                linea1.SetLeading(0f, 2.4f);
                linea1.SpacingAfter = 4f;
                doc.Add(linea1);

                var linea2 = new iTextSharp.text.Paragraph("________________________________, designo al Sr./Sra. ______________________________", normalFont);
                linea2.SetLeading(0f, 2.4f);
                linea2.SpacingAfter = 2f;
                doc.Add(linea2);

                var cuerpo = new iTextSharp.text.Paragraph
                {
                    Alignment = iTextSharp.text.Element.ALIGN_JUSTIFIED
                };
                cuerpo.SetLeading(0f, 2.0f);
                cuerpo.Add(new iTextSharp.text.Chunk("como Responsable Técnico (RT) para las estaciones regulares de Ecuador, comprometiéndome a mantener la coordinación necesaria con la DGAC.", normalFont));
                doc.Add(cuerpo);

                doc.Add(new iTextSharp.text.Paragraph(" "));

                var tabla = new iTextSharp.text.pdf.PdfPTable(2);
                tabla.WidthPercentage = 100;
                tabla.SetWidths(new float[] { 30f, 70f });
                tabla.SpacingBefore = 6f;

                var c1 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("Firma Director de Operaciones:", labelFont));
                c1.Border = iTextSharp.text.Rectangle.NO_BORDER;
                c1.PaddingBottom = 6f;
                tabla.AddCell(c1);

                var c2 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("______________________________________________", normalFont));
                c2.Border = iTextSharp.text.Rectangle.NO_BORDER;
                c2.PaddingBottom = 6f;
                tabla.AddCell(c2);

                var c3 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("Fecha:", labelFont));
                c3.Border = iTextSharp.text.Rectangle.NO_BORDER;
                tabla.AddCell(c3);

                var c4 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("____/____/________", normalFont));
                c4.Border = iTextSharp.text.Rectangle.NO_BORDER;
                tabla.AddCell(c4);

                doc.Add(tabla);

                doc.Close();
                var bytes = ms.ToArray();
                return File(bytes, "application/pdf", "Formulario_Designacion_RT.pdf");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult RegistrarDeclaracionTemporal(
            string correo,
            string tipoIdentificacion,
            string identificacion,
            string ruc,
            string nombres,
            string apellidos,
            string empresaCodigo,
            string empresaNombre,
            bool aceptada)
        {
            try
            {
                var email = (correo ?? string.Empty).Trim().ToLower();
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Json(new { success = false, message = "Correo requerido para registrar declaración." });
                }

                var identificacionFinal = (tipoIdentificacion == "RUC") ? ruc : identificacion;

                var dao = new DeclaracionTemporalDAO();
                if (!aceptada)
                {
                    dao.DeleteByEmail(email);
                    return Json(new { success = true });
                }

                dao.Upsert(new DeclaracionTemporal
                {
                    Email = email,
                    Identificacion = (identificacionFinal ?? string.Empty).Trim(),
                    EmpresaCodigo = (empresaCodigo ?? string.Empty).Trim(),
                    EmpresaNombre = (empresaNombre ?? string.Empty).Trim(),
                    Nombres = (nombres ?? string.Empty).Trim(),
                    Apellidos = (apellidos ?? string.Empty).Trim(),
                    Aceptada = true,
                    Ip = Request.UserHostAddress,
                    UserAgent = Request.UserAgent
                });

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al registrar declaración temporal: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DescargarDeclaracionResponsabilidad(string nombreCompleto, string empresa)
        {
            var nombre = (nombreCompleto ?? "").Trim();
            var empresaTxt = (empresa ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nombre)) nombre = "__________________________";
            if (string.IsNullOrWhiteSpace(empresaTxt)) empresaTxt = "__________________________";

            using (var ms = new MemoryStream())
            {
                var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 25f, 25f, 120f, 80f);
                var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);
                var server = System.Web.HttpContext.Current != null ? System.Web.HttpContext.Current.Server : null;
                writer.PageEvent = PdfBrandingHelper.CreateITextPageEvent(server, "UsuarioController.DescargarDeclaracionResponsabilidad");
                doc.Open();

                var titleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14);
                var normalFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 11);
                var labelFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 10);

                var titulo = new iTextSharp.text.Paragraph("DECLARACIÓN DE RESPONSABILIDAD", titleFont);
                titulo.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                titulo.SpacingAfter = 14f;
                doc.Add(titulo);

                var cuerpo = new iTextSharp.text.Paragraph
                {
                    Alignment = iTextSharp.text.Element.ALIGN_JUSTIFIED
                };
                cuerpo.SetLeading(0f, 1.8f);
                cuerpo.Add(new iTextSharp.text.Chunk("Yo, ", normalFont));
                cuerpo.Add(new iTextSharp.text.Chunk(nombre.ToUpperInvariant(), normalFont));
                cuerpo.Add(new iTextSharp.text.Chunk(" declaro conocer las políticas y procedimientos técnicos y operativos de la compañía ", normalFont));
                cuerpo.Add(new iTextSharp.text.Chunk(empresaTxt.ToUpperInvariant(), normalFont));
                cuerpo.Add(new iTextSharp.text.Chunk(" aplicables en las estaciones regulares de Ecuador. Asumo la responsabilidad como RT de mantener comunicación directa con la DGAC del Ecuador, a fin de gestionar los trámites de emisión, renovación o modificación del AOCR; así como también, de mantener la supervisión de las empresas contratadas para la asistencia técnica en tierra a sus aeronaves en los aeropuertos de Ecuador.", normalFont));
                doc.Add(cuerpo);

                doc.Add(new iTextSharp.text.Paragraph(" "));

                var tabla = new iTextSharp.text.pdf.PdfPTable(2);
                tabla.WidthPercentage = 100;
                tabla.SetWidths(new float[] { 30f, 70f });
                tabla.SpacingBefore = 6f;

                var c1 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("Firma:", labelFont));
                c1.Border = iTextSharp.text.Rectangle.NO_BORDER;
                c1.PaddingBottom = 6f;
                tabla.AddCell(c1);

                var c2 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("______________________________________________", normalFont));
                c2.Border = iTextSharp.text.Rectangle.NO_BORDER;
                c2.PaddingBottom = 6f;
                tabla.AddCell(c2);

                var c3 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("Fecha:", labelFont));
                c3.Border = iTextSharp.text.Rectangle.NO_BORDER;
                tabla.AddCell(c3);

                var c4 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("____/____/________", normalFont));
                c4.Border = iTextSharp.text.Rectangle.NO_BORDER;
                tabla.AddCell(c4);

                doc.Add(tabla);

                doc.Close();
                return File(ms.ToArray(), "application/pdf", "Declaracion_Responsabilidad_RT.pdf");
            }
        }

        // =====================================================
        // CÓDIGO ÚNICO DE USUARIO
        // Formato: 1ª letra del primer nombre
        //         + 2ª letra del segundo nombre (si existe)
        //         + apellidos (sin espacios)
        //         Máximo 10 caracteres (límite USUCOD en AS400)
        // =====================================================
        private string GenerarCodigoUsuarioUnico(string nombres, string apellidos)
        {
            if (string.IsNullOrWhiteSpace(nombres) && string.IsNullOrWhiteSpace(apellidos))
                return null;

            var baseCode = ConstruirBaseCodigoUsuario(nombres, apellidos);
            if (string.IsNullOrWhiteSpace(baseCode))
                return null;

            // Sin conflicto
            if (!UsuarioDAO.ExisteIdentificacion(baseCode))
                return baseCode;

            // Sufijo numérico dentro del límite de 10 chars
            for (int i = 1; i <= 999; i++)
            {
                var sufijo = i.ToString();
                var maxBase = 10 - sufijo.Length;
                var candidato = (baseCode.Length > maxBase ? baseCode.Substring(0, maxBase) : baseCode) + sufijo;
                if (!UsuarioDAO.ExisteIdentificacion(candidato))
                    return candidato;
            }

            return null;
        }

        private static string ConstruirBaseCodigoUsuario(string nombres, string apellidos)
        {
            // Quitar tildes y dejar solo letras/dígitos
            string Normalizar(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                var normalized = s.Trim().ToUpperInvariant()
                    .Replace('Á','A').Replace('É','E').Replace('Í','I')
                    .Replace('Ó','O').Replace('Ú','U').Replace('Ü','U')
                    .Replace('Ñ','N');
                return new string(normalized.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
            }

            var partes = Normalizar(nombres).Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);

            // 1ª letra del primer nombre
            char c1 = partes.Length > 0 && partes[0].Length > 0 ? partes[0][0] : '\0';

            // 1ª letra del segundo nombre (si existe)
            char c2 = partes.Length > 1 && partes[1].Length > 0 ? partes[1][0] : '\0';

            // Solo el primer apellido
            var partesApe = Normalizar(apellidos).Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
            var ape = partesApe.Length > 0 ? partesApe[0] : string.Empty;

            var sb = new System.Text.StringBuilder();
            if (c1 != '\0') sb.Append(c1);
            if (c2 != '\0') sb.Append(c2);
            sb.Append(ape);

            var resultado = sb.ToString();
            if (resultado.Length > 10) resultado = resultado.Substring(0, 10);

            return resultado;
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
                var passwordHashLegacy = "6aed143f116b7cb39338ecdfa1e56e334865c869db4469c35eacf5bdaef2046c";
                if (SyncUsuariosAs400Enabled())
                {
                    var usuarioAs400 = UsuarioAs400Record.CrearBasico(
                        cedula,
                        nombres?.Trim(),
                        apellidos?.Trim(),
                        "CI",
                        cedula,
                        correo?.Trim(),
                        passwordHashLegacy,
                        "AOCR");

                    string as400Error;
                    var as400Dao = new UsuarioAS400DAO();
                    if (!as400Dao.UpsertUsuarioCompleto(usuarioAs400, out as400Error))
                    {
                        return Json(new { success = false, message = "Error al registrar usuario en AS400: " + as400Error });
                    }
                }

                Usuario nuevoUsuario = new Usuario
                {
                    NombreUsuario = cedula,              // Login = Cédula
                    Email = correo,
                    // Unimos Nombres y Apellidos para el NombreCompleto
                    NombreCompleto = $"{nombres} {apellidos}".Trim().ToUpper(),
                    Contrasena = passwordHashLegacy, // Hash por defecto
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

        private static bool SyncUsuariosAs400Enabled()
        {
            var flag = ConfigurationManager.AppSettings["AS400:Usuarios:SyncOnRegister"];
            if (string.IsNullOrWhiteSpace(flag))
            {
                return false;
            }
            return flag.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================
        // REVISIÓN DE DESIGNACIONES RT POR COORDINADOR
        // =====================================================
        [HttpGet]
        [Authorize(Roles = "Administrador,CoordinacionLegal,JefaturaTecnica")]
        public ActionResult RevisarDesignaciones()
        {
            var usuarios = UsuarioDAO.ObtenerUsuariosRTParaRevision();
            return View("RevisarDesignaciones", usuarios);
        }

        [HttpGet]
        [Authorize(Roles = "Administrador,CoordinacionLegal,JefaturaTecnica")]
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
        [Authorize(Roles = "Administrador,CoordinacionLegal,JefaturaTecnica")]
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
        [Authorize(Roles = "Administrador,CoordinacionLegal,JefaturaTecnica")]
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
            string mensajeCorreo;
            var correoEnviado = UsuarioBL.NotificarAceptacionConClaveTemporal(
                usuario.Email,
                usuario.NombreCompleto,
                usuario.CodigoUsuario,
                out mensajeCorreo
            );

            if (correoEnviado)
                TempData["msg"] = "Designación aceptada, constancia generada y correo enviado con clave temporal.";
            else
                TempData["msg"] = "Designación aceptada y constancia generada. " + (mensajeCorreo ?? "");
            return RedirectToAction("RevisarDesignaciones");
        }

        [HttpPost]
        [Authorize(Roles = "Administrador,CoordinacionLegal,JefaturaTecnica")]
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

        [HttpPost]
        [Authorize(Roles = "Administrador,CoordinacionLegal,JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult InactivarUsuarioRT(int id)
        {
            bool actualizado = UsuarioDAO.ActualizarEstadoActividad(id, false);
            if (actualizado)
                TempData["msg"] = "Usuario inactivado correctamente.";
            else
                TempData["error"] = "No se pudo inactivar el usuario.";

            return RedirectToAction("RevisarDesignaciones");
        }

        [HttpPost]
        [Authorize(Roles = "Administrador,CoordinacionLegal,JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult ActivarUsuarioRT(int id)
        {
            bool actualizado = UsuarioDAO.ActualizarEstadoActividad(id, true);
            if (actualizado)
                TempData["msg"] = "Usuario activado correctamente.";
            else
                TempData["error"] = "No se pudo activar el usuario.";

            return RedirectToAction("RevisarDesignaciones");
        }

        [HttpPost]
        [Authorize(Roles = "Administrador,CoordinacionLegal,JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarUsuarioRT(int id)
        {
            var usuarioAntes = UsuarioDAO.ObtenerPorId(id);
            string mensaje;
            bool permitirPurgaDatosPruebas =
                Request != null &&
                Request.IsLocal &&
                System.Web.HttpContext.Current != null &&
                System.Web.HttpContext.Current.IsDebuggingEnabled;

            bool eliminado = UsuarioDAO.EliminarUsuarioRT(id, out mensaje, permitirPurgaDatosPruebas);
            var usuarioDespues = UsuarioDAO.ObtenerPorId(id);

            if (usuarioDespues != null)
            {
                // El botón "Eliminar" no debe terminar cambiando el estado del usuario.
                if (usuarioAntes != null && usuarioAntes.Activo != usuarioDespues.Activo)
                {
                    UsuarioDAO.ActualizarEstadoActividad(id, usuarioAntes.Activo);
                }

                eliminado = false;
                if (string.IsNullOrWhiteSpace(mensaje))
                {
                    mensaje = "No se pudo eliminar el usuario porque tiene informacion relacionada.";
                }
            }

            if (eliminado)
                TempData["msg"] = string.IsNullOrWhiteSpace(mensaje) ? "Usuario eliminado correctamente." : mensaje;
            else
                TempData["error"] = string.IsNullOrWhiteSpace(mensaje) ? "No se pudo eliminar el usuario." : mensaje;

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
