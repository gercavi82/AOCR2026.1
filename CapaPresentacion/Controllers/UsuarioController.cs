using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Configuration;
using CapaModelo;
using CapaModelo.RT.ViewModels;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaNegocio.Integraciones.As400Sync;
using CapaNegocio.Services;
using CapaPresentacion.Models.RT;
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
                var empresaTexto = Request.Form["EmpresaTexto"];
                NormalizarNombresApellidos(ref nombres, ref apellidos);
                var esRepresentanteValue = (Request.Form["esRepresentanteLegal"] ?? "").Trim();
                var esRepresentante = esRepresentanteValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                      esRepresentanteValue.Equals("on", StringComparison.OrdinalIgnoreCase);
                var aceptaDeclaracionValue = (Request.Form["aceptaDeclaracion"] ?? "").Trim();
                var aceptaDeclaracion = aceptaDeclaracionValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                        aceptaDeclaracionValue.Equals("on", StringComparison.OrdinalIgnoreCase);

                // Companías representadas (nuevo esquema multi-compañía).
                var companiasFormulario = ExtraerCompaniasFormulario();
                if (!string.IsNullOrWhiteSpace(empresaCodigo))
                {
                    var codigoPrincipal = (empresaCodigo ?? string.Empty).Trim().ToUpperInvariant();
                    if (!companiasFormulario.Any(c => string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), codigoPrincipal, StringComparison.OrdinalIgnoreCase)))
                    {
                        companiasFormulario.Add(new UsuarioCompaniaRT
                        {
                            CompaniaCodigo = codigoPrincipal,
                            CompaniaNombre = (empresaTexto ?? string.Empty).Trim()
                        });
                    }
                }

                companiasFormulario = companiasFormulario
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CompaniaCodigo))
                    .GroupBy(c => (c.CompaniaCodigo ?? string.Empty).Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
                var companiasDeclaracion = ConstruirCompaniasDeclaracion(companiasFormulario);

                // Identificación según tipo (CI o RUC)
                var identificacionFinal = (tipoIdentificacion == "RUC") ? ruc : identificacion;
                var nombreCompletoUsuario = string.Format("{0} {1}", nombres ?? string.Empty, apellidos ?? string.Empty).Trim().ToUpperInvariant();
                var textoDeclaracionFinal = ConstruirTextoDeclaracionResponsabilidad(nombreCompletoUsuario, companiasDeclaracion);

                // 2. VALIDAR DATOS REQUERIDOS
                if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(identificacionFinal) ||
                    string.IsNullOrWhiteSpace(nombres) || string.IsNullOrWhiteSpace(apellidos) ||
                    companiasFormulario.Count == 0 || companiasDeclaracion.Count == 0)
                {
                    return Json(new { success = false, message = "Debe seleccionar al menos una compañía a representar." });
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
                    CodigoUsuario = codigoUsuarioFinal,
                    // Persistir nombres y apellidos por separado en la tabla usuario.
                    NombreUsuario = (nombres ?? string.Empty).Trim().ToUpperInvariant(),
                    ApellidoUsuario = (apellidos ?? string.Empty).Trim().ToUpperInvariant(),
                    Email = correo,
                    NombreCompleto = nombreCompletoUsuario,
                    Contrasena = passwordHash, // Hash de contraseña temporal
                    Activo = true,
                    Rol = "Solicitante", // Rol por defecto para usuarios externos
                    EmpresaCodigo = companiasDeclaracion[0].Codigo,
                    RutaDocumentoLegal = rutaDocumento
                };

                // 5. GUARDAR USUARIO EN BASE DE DATOS
                int usuarioId = UsuarioDAO.Crear(nuevoUsuario);

                if (usuarioId <= 0)
                {
                    return Json(new { success = false, message = "No se pudo crear el usuario" });
                }

                // Guardar asignaciones multi-compañía del RT y sincronizar empresa principal legacy.
                var daoCompaniasRt = new UsuarioCompaniaRTDAO();
                var guardadoCompanias = daoCompaniasRt.GuardarAsignaciones(
                    usuarioId,
                    companiasFormulario,
                    codigoUsuarioFinal,
                    true);
                var codigosCompaniaSeleccionados = companiasDeclaracion
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Codigo))
                    .Select(c => (c.Codigo ?? string.Empty).Trim().ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (!guardadoCompanias)
                {
                    var legacyCodigos = string.Join(",", codigosCompaniaSeleccionados);
                    UsuarioDAO.ActualizarEmpresaCodigoPrincipal(usuarioId, legacyCodigos);
                    LogBL.RegistrarError(
                        "[Usuario/Crear] No se pudo persistir tabla relacional de compañías RT. Se aplicó fallback legacy multi-compañía.",
                        "usuarioId=" + usuarioId + ", codigos=" + legacyCodigos,
                        "UsuarioController");
                }
                else
                {
                    UsuarioDAO.ActualizarEmpresaCodigoPrincipal(usuarioId, companiasDeclaracion[0].Codigo);
                }

                // Marcar designación RT como pendiente y registrar ruta del documento
                UsuarioDAO.ActualizarDesignacionRT(usuarioId, rutaDocumento);

                // 5.1 Guardar aceptación de declaración en BD (RT) y notificar por correo
                bool declaracionRegistrada = false;
                bool declaracionHistorialRegistrada = false;
                bool pdfDeclaracionGenerado = false;
                bool correoDeclaracionEnviado = false;
                try
                {
                    var nombreEmpresaPrincipal = companiasDeclaracion[0].Nombre;

                    var rtService = new RTService();
                    var solicitudExistente = rtService.GetSolicitudByUsuario(usuarioId);
                    int solicitudId;

                    if (solicitudExistente == null)
                    {
                        var registroVm = new CapaModelo.RT.ViewModels.RegistroRTVM
                        {
                            RazonSocial = nombreEmpresaPrincipal,
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

                    rtService.AceptarDeclaracion(solicitudId, usuarioId, textoDeclaracionFinal);
                    declaracionRegistrada = true;

                    byte[] pdfDeclaracion = null;
                    var fechaAceptacion = DateTime.Now;
                    var nombreAdjunto = string.Format(
                        "Declaracion_Responsabilidad_RT_{0}_{1:yyyyMMddHHmmss}.pdf",
                        codigoUsuarioFinal,
                        fechaAceptacion);

                    try
                    {
                        pdfDeclaracion = GenerarPdfDeclaracionResponsabilidad(
                            nombreCompletoUsuario,
                            (identificacionFinal ?? string.Empty).Trim(),
                            companiasDeclaracion,
                            textoDeclaracionFinal,
                            fechaAceptacion);

                        pdfDeclaracionGenerado = pdfDeclaracion != null && pdfDeclaracion.Length > 0;
                    }
                    catch (Exception exPdf)
                    {
                        LogBL.RegistrarError(
                            "Error generando PDF de declaración de responsabilidad en Usuario/Crear.",
                            exPdf.ToString(),
                            "UsuarioController");
                        pdfDeclaracionGenerado = false;
                    }

                    try
                    {
                        var companiasHtml = ConstruirCompaniasHtmlCorreo(companiasDeclaracion);
                        var asuntoDecl = "Declaración de responsabilidad aceptada - Sistema AOCR";
                        var cuerpoDecl = $@"
                            <div style='font-family:Arial,sans-serif; font-size:14px; color:#222;'>
                                <p>Estimado/a {HttpUtility.HtmlEncode(nombreCompletoUsuario)},</p>
                                <p>Hemos registrado la <strong>aceptación</strong> de su declaración de responsabilidad RT.</p>
                                <p><strong>Trámite:</strong> Solicitud RT #{solicitudId}</p>
                                <p><strong>Fecha de aceptación:</strong> {fechaAceptacion:dd/MM/yyyy HH:mm}</p>
                                <p><strong>Compañías declaradas:</strong></p>
                                {companiasHtml}
                                <p>Su solicitud queda en proceso de validación por la DGAC.</p>
                                <hr />
                                <small>Este es un correo automático, por favor no responder.</small>
                            </div>";

                        var servicioCorreoDecl = new EnviarCorreo();
                        if (pdfDeclaracionGenerado)
                        {
                            correoDeclaracionEnviado = servicioCorreoDecl.enviaMensajeCorreoConAdjunto(
                                correo,
                                asuntoDecl,
                                cuerpoDecl,
                                pdfDeclaracion,
                                nombreAdjunto,
                                "application/pdf");
                        }
                        else
                        {
                            correoDeclaracionEnviado = servicioCorreoDecl.enviaMensajeCorreo(correo, asuntoDecl, cuerpoDecl);
                        }
                    }
                    catch (Exception exCorreoDeclaracion)
                    {
                        LogBL.RegistrarError(
                            "Error enviando correo de declaración de responsabilidad en Usuario/Crear.",
                            exCorreoDeclaracion.ToString(),
                            "UsuarioController");
                        correoDeclaracionEnviado = false;
                    }
                }
                catch (Exception exDeclaracion)
                {
                    LogBL.RegistrarError(
                        "Error registrando aceptación de declaración de responsabilidad en Usuario/Crear.",
                        exDeclaracion.ToString(),
                        "UsuarioController");
                    declaracionRegistrada = false;
                }

                try
                {
                    var historialDao = new DeclaracionTemporalDAO();
                    historialDao.InsertarHistorial(new DeclaracionTemporal
                    {
                        Email = (correo ?? string.Empty).Trim().ToLower(),
                        Identificacion = (identificacionFinal ?? string.Empty).Trim(),
                        EmpresaCodigo = string.Join(",", codigosCompaniaSeleccionados),
                        EmpresaNombre = string.Join(" | ", companiasDeclaracion.Select(FormatearCompaniaDeclaracion)),
                        Nombres = (nombres ?? string.Empty).Trim(),
                        Apellidos = (apellidos ?? string.Empty).Trim(),
                        Aceptada = true,
                        Ip = Request != null ? Request.UserHostAddress : string.Empty,
                        UserAgent = Request != null ? Request.UserAgent : string.Empty,
                        FinalizedAt = DateTime.Now
                    });

                    declaracionHistorialRegistrada = true;
                    if (!declaracionRegistrada)
                    {
                        declaracionRegistrada = true;
                    }
                }
                catch (Exception exHistorial)
                {
                    LogBL.RegistrarError(
                        "Error registrando historial de declaración en Usuario/Crear.",
                        exHistorial.ToString(),
                        "UsuarioController");
                }

                // 5. SI ES REPRESENTANTE LEGAL, PROCESAR ARCHIVOS DE REPRESENTACIÓN (opcional).
                if (esRepresentante)
                {
                    var archivos = new List<string>();
                    int index = 0;
                    while (Request.Form[$"Companias[{index}].IdCompania"] != null)
                    {
                        var codigoCompania = Request.Form[$"Companias[{index}].IdCompania"];
                        if (!string.IsNullOrWhiteSpace(codigoCompania))
                        {
                            // Procesar archivo asociado
                            var archivo = Request.Files[$"Companias[{index}].ArchivoRepresentante"];
                            if (archivo != null && archivo.ContentLength > 0)
                            {
                                string rutaArchivo = GuardarArchivoRepresentante(archivo, identificacionFinal, index);
                                if (!string.IsNullOrEmpty(rutaArchivo))
                                {
                                    archivos.Add(rutaArchivo);
                                }
                            }
                        }

                        index++;
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
                else
                {
                    if (!pdfDeclaracionGenerado)
                    {
                        mensajeFinal += " La aceptación se registró, pero no se pudo generar el PDF de la declaración.";
                    }
                    if (!correoDeclaracionEnviado)
                    {
                        mensajeFinal += " La aceptación se registró, pero no se pudo enviar el correo de declaración.";
                    }
                }

                if (!declaracionHistorialRegistrada)
                {
                    mensajeFinal += " No se pudo registrar el historial de declaración para respaldo.";
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
            string companiasJson,
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
                var companiasDeclaracion = ParsearCompaniasDeclaracion(companiasJson);
                if (aceptada && companiasDeclaracion.Count == 0 && string.IsNullOrWhiteSpace(empresaCodigo))
                {
                    return Json(new { success = false, message = "Debe seleccionar al menos una compañía para registrar la declaración." });
                }

                var empresaCodigoTemporal = companiasDeclaracion.Count > 0
                    ? companiasDeclaracion[0].Codigo
                    : (empresaCodigo ?? string.Empty).Trim();

                var empresaNombreTemporal = companiasDeclaracion.Count > 0
                    ? string.Join(" | ", companiasDeclaracion.Select(c => FormatearCompaniaDeclaracion(c)))
                    : (empresaNombre ?? string.Empty).Trim();

                if (empresaNombreTemporal.Length > 200)
                {
                    empresaNombreTemporal = empresaNombreTemporal.Substring(0, 200);
                }

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
                    EmpresaCodigo = empresaCodigoTemporal,
                    EmpresaNombre = empresaNombreTemporal,
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
        public ActionResult DescargarDeclaracionResponsabilidad(string nombreCompleto, string companiasJson, string empresa, string identificacion)
        {
            var nombre = (nombreCompleto ?? "").Trim();
            var companiasDeclaracion = ParsearCompaniasDeclaracion(companiasJson);
            if (companiasDeclaracion.Count == 0 && !string.IsNullOrWhiteSpace(empresa))
            {
                companiasDeclaracion.Add(new CompaniaDeclaracionItem
                {
                    Codigo = string.Empty,
                    Nombre = (empresa ?? string.Empty).Trim()
                });
            }

            if (string.IsNullOrWhiteSpace(nombre)) nombre = "__________________________";
            if (companiasDeclaracion.Count == 0)
            {
                companiasDeclaracion.Add(new CompaniaDeclaracionItem
                {
                    Codigo = string.Empty,
                    Nombre = "__________________________"
                });
            }

            var textoDeclaracion = ConstruirTextoDeclaracionResponsabilidad(nombre, companiasDeclaracion);
            var pdfBytes = GenerarPdfDeclaracionResponsabilidad(
                nombre,
                (identificacion ?? string.Empty).Trim(),
                companiasDeclaracion,
                textoDeclaracion,
                DateTime.Now);

            return File(pdfBytes, "application/pdf", "Declaracion_Responsabilidad_RT.pdf");
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
                NormalizarNombresApellidos(ref nombres, ref apellidos);

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
                    CodigoUsuario = cedula, // Login = cédula
                    NombreUsuario = (nombres ?? string.Empty).Trim().ToUpperInvariant(),
                    ApellidoUsuario = (apellidos ?? string.Empty).Trim().ToUpperInvariant(),
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
        public ActionResult GestionarCompaniasRT(int id)
        {
            var usuario = UsuarioDAO.ObtenerPorId(id);
            if (usuario == null)
            {
                TempData["error"] = "Usuario no encontrado.";
                return RedirectToAction("RevisarDesignaciones");
            }

            var daoCompanias = new UsuarioCompaniaRTDAO();
            var asignadas = daoCompanias.ObtenerCompaniasAsignadas(id)
                .Select(c => (c.CompaniaCodigo ?? string.Empty).Trim().ToUpperInvariant())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (asignadas.Count == 0)
            {
                var codigosLegacy = ParsearCodigosCompaniaLegacy(usuario.EmpresaCodigo);
                foreach (var codigo in codigosLegacy)
                {
                    if (!asignadas.Contains(codigo, StringComparer.OrdinalIgnoreCase))
                    {
                        asignadas.Add(codigo);
                    }
                }
            }

            var vm = new GestionCompaniasRTViewModel
            {
                UsuarioId = usuario.Id,
                CodigoUsuario = usuario.CodigoUsuario,
                NombreUsuario = usuario.NombreCompleto,
                Correo = usuario.Email,
                EstadoDesignacionRt = usuario.EstadoDesignacionRT,
                CompaniasSeleccionadas = asignadas,
                CatalogoCompanias = ConstruirCatalogoCompaniasSelect(asignadas)
            };

            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "Administrador,CoordinacionLegal,JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult GestionarCompaniasRT(GestionCompaniasRTViewModel model)
        {
            if (model == null || model.UsuarioId <= 0)
            {
                TempData["error"] = "Datos inválidos para actualizar compañías RT.";
                return RedirectToAction("RevisarDesignaciones");
            }

            var usuario = UsuarioDAO.ObtenerPorId(model.UsuarioId);
            if (usuario == null)
            {
                TempData["error"] = "Usuario no encontrado.";
                return RedirectToAction("RevisarDesignaciones");
            }

            var codigos = (model.CompaniasSeleccionadas ?? new List<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codigos.Count == 0)
            {
                ModelState.AddModelError("", "Debe seleccionar al menos una compañía.");
                model.CatalogoCompanias = ConstruirCatalogoCompaniasSelect(codigos);
                return View(model);
            }

            var catalogo = ConstruirCatalogoCompaniasSelect(codigos);
            var lookupNombres = catalogo
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Value))
                .ToDictionary(c => c.Value.Trim().ToUpperInvariant(), c => (c.Text ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);

            var asignaciones = codigos.Select(codigo => new UsuarioCompaniaRT
            {
                UsuarioId = model.UsuarioId,
                CompaniaCodigo = codigo,
                CompaniaNombre = lookupNombres.ContainsKey(codigo) ? lookupNombres[codigo] : codigo,
                Activo = true
            }).ToList();

            var daoCompanias = new UsuarioCompaniaRTDAO();
            var actor = User != null ? User.Identity.Name : "sistema";
            var ok = daoCompanias.GuardarAsignaciones(model.UsuarioId, asignaciones, actor, true);
            if (!ok)
            {
                ModelState.AddModelError("", "No se pudo guardar la relación usuario RT - compañías.");
                model.CatalogoCompanias = catalogo;
                return View(model);
            }

            UsuarioDAO.ActualizarEmpresaCodigoPrincipal(model.UsuarioId, codigos[0]);

            TempData["msg"] = "Compañías del usuario RT actualizadas correctamente.";
            return RedirectToAction("RevisarDesignaciones");
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

            // Compatibilidad: garantizar al menos una compañía asignada para RT.
            var daoCompaniasRt = new UsuarioCompaniaRTDAO();
            var companiasAsignadas = daoCompaniasRt.ObtenerCompaniasAsignadas(id);
            if (companiasAsignadas.Count == 0)
            {
                var codigosLegacy = ParsearCodigosCompaniaLegacy(usuario.EmpresaCodigo);
                var nombreCompaniaLegacy = ResolverNombreCompaniaUsuario(usuario);
                foreach (var codigoCompania in codigosLegacy)
                {
                    daoCompaniasRt.AgregarCompania(id, codigoCompania, nombreCompaniaLegacy, User != null ? User.Identity.Name : "sistema");
                }
                companiasAsignadas = daoCompaniasRt.ObtenerCompaniasAsignadas(id);
            }

            if (companiasAsignadas.Count > 0)
            {
                UsuarioDAO.ActualizarEmpresaCodigoPrincipal(id, companiasAsignadas[0].CompaniaCodigo);
            }

            var nombreCompania = companiasAsignadas.Count > 0
                ? (!string.IsNullOrWhiteSpace(companiasAsignadas[0].CompaniaNombre)
                    ? companiasAsignadas[0].CompaniaNombre
                    : companiasAsignadas[0].CompaniaCodigo)
                : ResolverNombreCompaniaUsuario(usuario);
            string mensajeCorreo;
            var correoEnviado = UsuarioBL.NotificarAceptacionConClaveTemporal(
                usuario.Email,
                usuario.NombreCompleto,
                usuario.CodigoUsuario,
                nombreCompania,
                out mensajeCorreo
            );

            if (correoEnviado)
                TempData["msg"] = "Designación aceptada, constancia generada y correo enviado con clave temporal.";
            else
                TempData["msg"] = "Designación aceptada y constancia generada. " + (mensajeCorreo ?? "");
            return RedirectToAction("RevisarDesignaciones");
        }

        private static string ResolverNombreCompaniaUsuario(Usuario usuario)
        {
            if (usuario == null)
            {
                return string.Empty;
            }

            var codigoEmpresa = (usuario.EmpresaCodigo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(codigoEmpresa))
            {
                return string.Empty;
            }

            try
            {
                bool preferirMirror;
                if (bool.TryParse(ConfigurationManager.AppSettings["Sync:Mirror:PreferReadForEmpresas"], out preferirMirror) && preferirMirror)
                {
                    var mirror = new MirrorReadService();
                    var empresaMirror = mirror.ListarCompaniasActivas(5000)
                        .FirstOrDefault(x => x != null &&
                            string.Equals((x.CodigoOaci ?? string.Empty).Trim(), codigoEmpresa, StringComparison.OrdinalIgnoreCase));

                    if (empresaMirror != null && !string.IsNullOrWhiteSpace(empresaMirror.NombreCompania))
                    {
                        return empresaMirror.NombreCompania.Trim();
                    }
                }
            }
            catch (Exception exMirror)
            {
                System.Diagnostics.Debug.WriteLine("Usuario/AceptarDesignacion: no se pudo resolver compañía desde mirror: " + exMirror.Message);
            }

            try
            {
                var empresaDao = new EmpresaAS400DAO();
                var empresa = empresaDao.ObtenerEmpresaPorCodigo(codigoEmpresa);
                if (empresa != null && !string.IsNullOrWhiteSpace(empresa.Nombre))
                {
                    return empresa.Nombre.Trim();
                }
            }
            catch (Exception exAs400)
            {
                System.Diagnostics.Debug.WriteLine("Usuario/AceptarDesignacion: no se pudo resolver compañía desde AS400: " + exAs400.Message);
            }

            return codigoEmpresa;
        }

        private static List<string> ParsearCodigosCompaniaLegacy(string empresaCodigo)
        {
            if (string.IsNullOrWhiteSpace(empresaCodigo))
            {
                return new List<string>();
            }

            return (empresaCodigo ?? string.Empty)
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => (x ?? string.Empty).Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<SelectListItem> ConstruirCatalogoCompaniasSelect(IEnumerable<string> seleccionadas)
        {
            var seleccionLookup = new HashSet<string>(
                (seleccionadas ?? Enumerable.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase);

            var items = new List<SelectListItem>();
            var catalogo = new List<Empresa>();
            try
            {
                var daoEmpresa = new EmpresaAS400DAO();
                catalogo = daoEmpresa.ObtenerEmpresas() ?? new List<Empresa>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GestionarCompaniasRT: error cargando catálogo AS400: " + ex.Message);
            }

            foreach (var empresa in catalogo
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.CodigoOaci))
                .OrderBy(e => e.Nombre ?? string.Empty))
            {
                var codigo = (empresa.CodigoOaci ?? string.Empty).Trim().ToUpperInvariant();
                var nombre = (empresa.Nombre ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(codigo))
                {
                    continue;
                }

                items.Add(new SelectListItem
                {
                    Value = codigo,
                    Text = nombre,
                    Selected = seleccionLookup.Contains(codigo)
                });
            }

            return items;
        }

        private List<CompaniaDeclaracionItem> ConstruirCompaniasDeclaracion(IEnumerable<UsuarioCompaniaRT> companiasFormulario)
        {
            var lista = (companiasFormulario ?? Enumerable.Empty<UsuarioCompaniaRT>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CompaniaCodigo))
                .Select(c => new CompaniaDeclaracionItem
                {
                    Codigo = (c.CompaniaCodigo ?? string.Empty).Trim().ToUpperInvariant(),
                    Nombre = (c.CompaniaNombre ?? string.Empty).Trim()
                })
                .GroupBy(c => c.Codigo, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            foreach (var compania in lista)
            {
                if (string.IsNullOrWhiteSpace(compania.Nombre) ||
                    string.Equals(compania.Nombre.Trim(), compania.Codigo, StringComparison.OrdinalIgnoreCase))
                {
                    var nombreResuelto = ResolverNombreCompaniaPorCodigoInterno(compania.Codigo);
                    if (!string.IsNullOrWhiteSpace(nombreResuelto))
                    {
                        compania.Nombre = nombreResuelto.Trim();
                    }
                }
            }

            return lista;
        }

        private string ResolverNombreCompaniaPorCodigoInterno(string codigoCompania)
        {
            var codigo = (codigoCompania ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return string.Empty;
            }

            try
            {
                var daoEmpresa = new EmpresaAS400DAO();
                var empresa = daoEmpresa.ObtenerEmpresaPorCodigo(codigo);
                if (empresa != null && !string.IsNullOrWhiteSpace(empresa.Nombre))
                {
                    return empresa.Nombre.Trim();
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError(
                    "No se pudo resolver el nombre de la compañía para la declaración.",
                    ex.ToString(),
                    "UsuarioController");
            }

            return codigo;
        }

        private static List<CompaniaDeclaracionItem> ParsearCompaniasDeclaracion(string companiasJson)
        {
            if (string.IsNullOrWhiteSpace(companiasJson))
            {
                return new List<CompaniaDeclaracionItem>();
            }

            try
            {
                var serializer = new JavaScriptSerializer();
                var items = serializer.Deserialize<List<CompaniaDeclaracionItem>>(companiasJson) ?? new List<CompaniaDeclaracionItem>();
                return items
                    .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Codigo))
                    .Select(i => new CompaniaDeclaracionItem
                    {
                        Codigo = (i.Codigo ?? string.Empty).Trim().ToUpperInvariant(),
                        Nombre = (i.Nombre ?? string.Empty).Trim()
                    })
                    .GroupBy(i => i.Codigo, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }
            catch
            {
                return new List<CompaniaDeclaracionItem>();
            }
        }

        private static string ConstruirTextoDeclaracionResponsabilidad(string nombreCompleto, IList<CompaniaDeclaracionItem> companias)
        {
            var nombre = string.IsNullOrWhiteSpace(nombreCompleto)
                ? "__________________________"
                : nombreCompleto.Trim().ToUpperInvariant();

            var listado = (companias ?? new List<CompaniaDeclaracionItem>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Codigo))
                .Select((c, index) => string.Format("{0}. {1}", index + 1, FormatearCompaniaDeclaracion(c)))
                .ToList();

            if (listado.Count == 0)
            {
                listado.Add("1. __________________________");
            }

            var sb = new StringBuilder();
            sb.Append("Yo, ");
            sb.Append(nombre);
            sb.Append(" declaro conocer las políticas y procedimientos técnicos y operativos aplicables en las estaciones regulares de Ecuador para las siguientes compañías:");
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(string.Join(Environment.NewLine, listado));
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("Asumo la responsabilidad como RT de mantener comunicación directa con la DGAC del Ecuador, a fin de gestionar los trámites de emisión, renovación o modificación del AOCR; así como también, de mantener la supervisión de las empresas contratadas para la asistencia técnica en tierra a sus aeronaves en los aeropuertos de Ecuador.");

            return sb.ToString();
        }

        private static string ConstruirCompaniasHtmlCorreo(IList<CompaniaDeclaracionItem> companias)
        {
            var items = (companias ?? new List<CompaniaDeclaracionItem>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Codigo))
                .Select(FormatearCompaniaDeclaracion)
                .ToList();

            if (items.Count == 0)
            {
                return "<p>No se registraron compañías en la declaración.</p>";
            }

            var sb = new StringBuilder();
            sb.Append("<ul>");
            foreach (var item in items)
            {
                sb.Append("<li>");
                sb.Append(HttpUtility.HtmlEncode(item));
                sb.Append("</li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static string FormatearCompaniaDeclaracion(CompaniaDeclaracionItem compania)
        {
            if (compania == null)
            {
                return "Compañía no especificada";
            }

            var codigo = (compania.Codigo ?? string.Empty).Trim().ToUpperInvariant();
            var nombre = (compania.Nombre ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(nombre))
            {
                return "Compañía no especificada";
            }

            if (string.IsNullOrWhiteSpace(codigo))
            {
                return nombre;
            }

            return "[" + codigo + "] " + (string.IsNullOrWhiteSpace(nombre) ? codigo : nombre);
        }

        private static byte[] GenerarPdfDeclaracionResponsabilidad(
            string nombreCompleto,
            string identificacion,
            IList<CompaniaDeclaracionItem> companias,
            string textoDeclaracion,
            DateTime fechaAceptacion)
        {
            using (var ms = new MemoryStream())
            {
                var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 36f, 36f, 130f, 90f);
                var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);
                var server = System.Web.HttpContext.Current != null ? System.Web.HttpContext.Current.Server : null;
                writer.PageEvent = PdfBrandingHelper.CreateITextPageEvent(server, "UsuarioController.GenerarPdfDeclaracionResponsabilidad");
                doc.Open();

                var titleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14);
                var subtitleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 11);
                var normalFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 10);
                var smallFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 9);

                var titulo = new iTextSharp.text.Paragraph("DECLARACIÓN DE RESPONSABILIDAD", titleFont)
                {
                    Alignment = iTextSharp.text.Element.ALIGN_CENTER,
                    SpacingAfter = 10f
                };
                doc.Add(titulo);

                var datos = new iTextSharp.text.pdf.PdfPTable(2)
                {
                    WidthPercentage = 100,
                    SpacingAfter = 8f
                };
                datos.SetWidths(new[] { 35f, 65f });

                AgregarFilaTabla(datos, "Representante técnico:", string.IsNullOrWhiteSpace(nombreCompleto) ? "N/D" : nombreCompleto.Trim().ToUpperInvariant(), normalFont);
                AgregarFilaTabla(datos, "Identificación:", string.IsNullOrWhiteSpace(identificacion) ? "N/D" : identificacion.Trim(), normalFont);
                AgregarFilaTabla(datos, "Fecha aceptación:", fechaAceptacion.ToString("dd/MM/yyyy HH:mm"), normalFont);
                AgregarFilaTabla(datos, "Referencia:", "DECL-RT-" + fechaAceptacion.ToString("yyyyMMddHHmmss"), normalFont);
                doc.Add(datos);

                var subtituloCompanias = new iTextSharp.text.Paragraph("Compañías declaradas", subtitleFont)
                {
                    SpacingAfter = 4f
                };
                doc.Add(subtituloCompanias);

                var companiasNormalizadas = (companias ?? new List<CompaniaDeclaracionItem>())
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Codigo))
                    .ToList();
                if (companiasNormalizadas.Count == 0)
                {
                    companiasNormalizadas.Add(new CompaniaDeclaracionItem
                    {
                        Codigo = string.Empty,
                        Nombre = "No especificada"
                    });
                }

                var listaCompanias = new iTextSharp.text.List(iTextSharp.text.List.ORDERED, 12f);
                foreach (var compania in companiasNormalizadas)
                {
                    listaCompanias.Add(new iTextSharp.text.ListItem(FormatearCompaniaDeclaracion(compania), normalFont));
                }
                doc.Add(listaCompanias);
                doc.Add(new iTextSharp.text.Paragraph(" "));

                var subtituloDeclaracion = new iTextSharp.text.Paragraph("Texto de la declaración", subtitleFont)
                {
                    SpacingAfter = 4f
                };
                doc.Add(subtituloDeclaracion);

                var cuerpo = new iTextSharp.text.Paragraph(textoDeclaracion ?? string.Empty, normalFont)
                {
                    Alignment = iTextSharp.text.Element.ALIGN_JUSTIFIED,
                    SpacingAfter = 16f
                };
                cuerpo.SetLeading(0f, 1.5f);
                doc.Add(cuerpo);

                doc.Add(new iTextSharp.text.Paragraph("______________________________________________", normalFont));
                doc.Add(new iTextSharp.text.Paragraph("Aceptación del Responsable Técnico", smallFont));
                doc.Add(new iTextSharp.text.Paragraph("Documento generado automáticamente por AOCR.", smallFont));

                doc.Close();
                return ms.ToArray();
            }
        }

        private static void AgregarFilaTabla(iTextSharp.text.pdf.PdfPTable tabla, string etiqueta, string valor, iTextSharp.text.Font font)
        {
            var cellEtiqueta = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(etiqueta, font))
            {
                Border = iTextSharp.text.Rectangle.BOX,
                Padding = 5f,
                BackgroundColor = new iTextSharp.text.BaseColor(244, 246, 248)
            };

            var cellValor = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(valor ?? string.Empty, font))
            {
                Border = iTextSharp.text.Rectangle.BOX,
                Padding = 5f
            };

            tabla.AddCell(cellEtiqueta);
            tabla.AddCell(cellValor);
        }

        private List<UsuarioCompaniaRT> ExtraerCompaniasFormulario()
        {
            var resultado = new List<UsuarioCompaniaRT>();
            if (Request == null || Request.Form == null)
            {
                return resultado;
            }

            var index = 0;
            while (Request.Form["Companias[" + index + "].IdCompania"] != null)
            {
                var codigo = (Request.Form["Companias[" + index + "].IdCompania"] ?? string.Empty).Trim().ToUpperInvariant();
                var nombre = (Request.Form["Companias[" + index + "].NombreCompania"] ?? string.Empty).Trim();

                if (!string.IsNullOrWhiteSpace(codigo))
                {
                    resultado.Add(new UsuarioCompaniaRT
                    {
                        CompaniaCodigo = codigo,
                        CompaniaNombre = nombre
                    });
                }

                index++;
            }

            return resultado;
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

        private static void NormalizarNombresApellidos(ref string nombres, ref string apellidos)
        {
            nombres = (nombres ?? string.Empty).Trim();
            apellidos = (apellidos ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(apellidos) || string.IsNullOrWhiteSpace(nombres))
            {
                return;
            }

            var partes = nombres
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (partes.Count <= 1)
            {
                return;
            }

            if (partes.Count == 2)
            {
                nombres = partes[0];
                apellidos = partes[1];
                return;
            }

            if (partes.Count == 3)
            {
                nombres = string.Join(" ", partes.Take(2));
                apellidos = partes[2];
                return;
            }

            nombres = string.Join(" ", partes.Take(partes.Count - 2));
            apellidos = string.Join(" ", partes.Skip(partes.Count - 2));
        }

        private class CompaniaDeclaracionItem
        {
            public string Codigo { get; set; }
            public string Nombre { get; set; }
        }
    }
}
