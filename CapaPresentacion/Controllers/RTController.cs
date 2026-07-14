using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using CapaModelo;
using CapaModelo.RT;
using CapaModelo.RT.ViewModels;
using CapaNegocio.Services;
using CapaNegocio.Helpers;
using CapaDatos.DAOs;
using CapaNegocio;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class RTController : Controller
    {
        private const string ROL_RT = "Solicitante";
        private const string ROL_RT_ALIAS = "RepresentanteTecnico";
        private const string ROL_ADMIN = "Administrador";
        private const int MAX_SUBSANACION_BYTES = 10 * 1024 * 1024;
        private readonly RTService _service = new RTService();
        private readonly SolicitudAOCRDAO _solicitudDAO = new SolicitudAOCRDAO();

        private bool TryObtenerUsuarioActualId(out int usuarioId){usuarioId=ObtenerUsuarioId();return usuarioId>0;}
        private bool EsPropietarioSolicitud(CapaModelo.SolicitudAOCR solicitud,int usuarioId){return solicitud!=null&&usuarioId>0&&(solicitud.CodigoUsuario==usuarioId||solicitud.UsuarioId==usuarioId||User.IsInRole(ROL_ADMIN));}

        private int ObtenerUsuarioId()
        {
            var v = Session["UserId"] ?? Session["IdUsuario"] ?? Session["CodigoUsuario"];
            if (v != null && int.TryParse(v.ToString(), out var id))
                return id;

            return 0;
        }

        private void CargarContextoSolicitudRt(SolicitudRTModel solicitud, int usuarioId)
        {
            var usuario = usuarioId > 0 ? UsuarioDAO.ObtenerPorId(usuarioId) : null;
            var estado = solicitud != null ? _service.NormalizarEstado(solicitud.Estado) : RTService.EstadoBorrador;
            var documento = solicitud != null ? _service.GetDocumentoDesignacion(solicitud.Id) : null;

            var rutaConstancia = usuario != null ? (usuario.RutaConstanciaRT ?? string.Empty).Trim() : string.Empty;
            var tieneConstancia = false;
            if (!string.IsNullOrWhiteSpace(rutaConstancia))
            {
                try
                {
                    var rutaFisica = Server.MapPath(rutaConstancia);
                    tieneConstancia = System.IO.File.Exists(rutaFisica);
                }
                catch
                {
                    tieneConstancia = false;
                }
            }

            ViewBag.EstadoRt = estado;
            ViewBag.SolicitudRtId = solicitud != null ? solicitud.Id : 0;
            ViewBag.EsEditableRt = solicitud == null || _service.EsEstadoEditable(estado);
            ViewBag.ObservacionCoordinadorRt = solicitud != null ? (solicitud.ObservacionCoordinador ?? string.Empty).Trim() : string.Empty;
            ViewBag.TieneDeclaracionRt = solicitud != null && solicitud.DeclaracionAceptada;
            ViewBag.TieneDocumentoRt = documento != null;
            ViewBag.NombreArchivoRt = documento != null ? documento.NombreArchivo : string.Empty;
            ViewBag.TieneConstanciaRt = tieneConstancia;
            ViewBag.EstadoDesignacionLegacyRt = usuario != null ? (usuario.EstadoDesignacionRT ?? string.Empty).Trim() : string.Empty;
        }

        [HttpGet]
        public ActionResult Registro()
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            var vm = new RegistroRTVM();
            var usuario = usuarioId > 0 ? UsuarioDAO.ObtenerPorId(usuarioId) : null;

            if (solicitud != null)
            {
                var compania = _service.GetCompaniaById(solicitud.CompaniaId);
                vm.SolicitudId = solicitud.Id;
                vm.RazonSocial = compania?.RazonSocial;
                vm.Ruc = compania?.Ruc;
                vm.Telefono = compania?.Telefono;
                vm.Email = compania?.EmailContacto;
                vm.AreaContableJson = compania?.AreaContableJson;

                ViewBag.Estado = solicitud.Estado;
            }
            else if (usuario != null)
            {
                vm.Email = usuario.Email;
            }

            CargarContextoSolicitudRt(solicitud, usuarioId);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GuardarRegistro(RegistroRTVM vm)
        {
            var usuarioId = ObtenerUsuarioId();

            if (!ModelState.IsValid)
            {
                CargarContextoSolicitudRt(_service.GetSolicitudByUsuario(usuarioId), usuarioId);
                return View("Registro", vm);
            }

            try
            {
                var solicitudId = _service.GuardarBorrador(vm, usuarioId);
                TempData["Ok"] = "Borrador guardado correctamente.";
                return RedirectToAction("Declaracion", new { solicitudId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                CargarContextoSolicitudRt(_service.GetSolicitudByUsuario(usuarioId), usuarioId);
                return View("Registro", vm);
            }
        }

        [HttpGet]
        public ActionResult Declaracion(int solicitudId)
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            if (solicitud == null || solicitud.Id != solicitudId)
            {
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction("Registro");
            }

            var vm = new DeclaracionRTVM
            {
                SolicitudId = solicitud.Id,
                TextoDeclaracion = solicitud.DeclaracionTexto,
                Acepto = solicitud.DeclaracionAceptada,
                Estado = solicitud.Estado
            };

            var compania = _service.GetCompaniaById(solicitud.CompaniaId);
            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            var nombre = (usuario != null && !string.IsNullOrWhiteSpace(usuario.NombreCompleto))
                ? usuario.NombreCompleto
                : (usuario != null ? usuario.NombreUsuario : "");

            var razonSocial = compania != null ? compania.RazonSocial : "";
            var textoPersonalizado = _service.ObtenerTextoDeclaracionPersonalizado(nombre, razonSocial);
            if (!string.IsNullOrWhiteSpace(textoPersonalizado))
            {
                vm.TextoDeclaracion = textoPersonalizado;
            }

            CargarContextoSolicitudRt(solicitud, usuarioId);

            return View(vm);
        }

        [HttpGet]
        public ActionResult DescargarDeclaracionPdf(int solicitudId, bool vistaPrevia = false)
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            if (solicitudId <= 0 && solicitud != null)
            {
                solicitudId = solicitud.Id;
            }
            if (solicitud == null || solicitud.Id != solicitudId)
            {
                LogBL.RegistrarError("Solicitud RT no encontrada para generar PDF. usuarioId=" + usuarioId + " solicitudId=" + solicitudId, "n/a", "RTController");
                return Content("Solicitud no encontrada para generar la declaración.");
            }

            var compania = _service.GetCompaniaById(solicitud.CompaniaId);
            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            var nombre = (usuario != null && !string.IsNullOrWhiteSpace(usuario.NombreCompleto))
                ? usuario.NombreCompleto
                : (usuario != null ? usuario.NombreUsuario : "");

            var razonSocial = compania != null ? compania.RazonSocial : "";
            var vm = new DeclaracionPdfVM
            {
                NombreCompleto = nombre,
                Compania = razonSocial,
                TextoDeclaracion = _service.ObtenerTextoDeclaracionPersonalizado(nombre, razonSocial),
                FechaEmision = DateTime.Now
            };

            var fileName = "Declaracion_RT_" + solicitudId + ".pdf";
            try
            {
                var pdfBytes = GenerarDeclaracionPdf(vm);
                return vistaPrevia
                    ? File(pdfBytes, "application/pdf")
                    : File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error generando PDF de declaración RT (iText).", ex.ToString(), "RTController");
                return Content("Error generando PDF. Revise logs.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AceptarDeclaracion(DeclaracionRTVM vm)
        {
            var usuarioId = ObtenerUsuarioId();
            if (!ModelState.IsValid)
            {
                CargarContextoSolicitudRt(_service.GetSolicitudByUsuario(usuarioId), usuarioId);
                return View("Declaracion", vm);
            }

            try
            {
                _service.AceptarDeclaracion(vm.SolicitudId, usuarioId);
                TempData["Ok"] = "Declaración aceptada.";
                return RedirectToAction("Designacion", new { solicitudId = vm.SolicitudId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                CargarContextoSolicitudRt(_service.GetSolicitudByUsuario(usuarioId), usuarioId);
                return View("Declaracion", vm);
            }
        }

        [HttpGet]
        public ActionResult Designacion(int solicitudId)
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            if (solicitud == null || solicitud.Id != solicitudId)
            {
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction("Registro");
            }

            var doc = _service.GetDocumentoDesignacion(solicitudId);
            var vm = new DesignacionUploadVM
            {
                SolicitudId = solicitud.Id,
                NombreArchivoActual = doc?.NombreArchivo,
                Estado = solicitud.Estado
            };

            CargarContextoSolicitudRt(solicitud, usuarioId);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubirDesignacion(DesignacionUploadVM vm)
        {
            var usuarioId = ObtenerUsuarioId();
            if (!ModelState.IsValid)
            {
                CargarContextoSolicitudRt(_service.GetSolicitudByUsuario(usuarioId), usuarioId);
                return View("Designacion", vm);
            }

            try
            {
                _service.SubirDesignacionPdf(vm.SolicitudId, usuarioId, vm.ArchivoPdf);
                TempData["Ok"] = "Documento cargado correctamente.";
                return RedirectToAction("Designacion", new { solicitudId = vm.SolicitudId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                CargarContextoSolicitudRt(_service.GetSolicitudByUsuario(usuarioId), usuarioId);
                return View("Designacion", vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Enviar(int solicitudId)
        {
            var usuarioId = ObtenerUsuarioId();
            try
            {
                _service.EnviarSolicitud(solicitudId, usuarioId);
                TempData["Ok"] = "Solicitud enviada. En proceso de validación y aprobación por Coordinador.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Designacion", new { solicitudId });
        }

        [HttpGet]
        public ActionResult DescargarConstancia()
        {
            var usuarioId = ObtenerUsuarioId();
            var usuario = usuarioId > 0 ? UsuarioDAO.ObtenerPorId(usuarioId) : null;
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.RutaConstanciaRT))
            {
                return HttpNotFound();
            }

            var rutaFisica = Server.MapPath(usuario.RutaConstanciaRT);
            if (!System.IO.File.Exists(rutaFisica))
            {
                return HttpNotFound();
            }

            var esPdf = string.Equals(Path.GetExtension(rutaFisica), ".pdf", StringComparison.OrdinalIgnoreCase);
            return File(
                rutaFisica,
                esPdf ? "application/pdf" : "text/plain",
                esPdf
                    ? "Constancia_RT_" + (usuario.CodigoUsuario ?? usuarioId.ToString()) + ".pdf"
                    : "Constancia_RT_" + (usuario.CodigoUsuario ?? usuarioId.ToString()) + ".txt");
        }

        private static byte[] GenerarDeclaracionPdf(DeclaracionPdfVM vm)
        {
            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 25f, 25f, 120f, 80f);
                var writer = PdfWriter.GetInstance(doc, ms);
                var server = System.Web.HttpContext.Current != null ? System.Web.HttpContext.Current.Server : null;
                writer.PageEvent = PdfBrandingHelper.CreateITextPageEvent(server, "RTController.GenerarDeclaracionPdf");
                doc.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 11);
                var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

                doc.Add(new Paragraph("Declaración de Responsabilidad", titleFont));
                doc.Add(new Paragraph("Responsable Técnico (RT)", normalFont));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(vm.TextoDeclaracion ?? "", normalFont));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("Nombre: " + (vm.NombreCompleto ?? ""), normalFont));
                doc.Add(new Paragraph("Compañía: " + (vm.Compania ?? ""), normalFont));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("_______________________________", normalFont));
                doc.Add(new Paragraph("Firma del Responsable Técnico", smallFont));
                doc.Add(new Paragraph("Fecha emisión: " + vm.FechaEmision.ToString("dd/MM/yyyy"), smallFont));

                doc.Close();
                return ms.ToArray();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SolicitarNuevaInspeccion(int codigoSolicitud)
        {
            var usuarioId = ObtenerUsuarioId();
            var resultado = new CapaNegocio.Services.NuevaInspeccionPorNcService()
                .Crear(codigoSolicitud, usuarioId, User.Identity.Name ?? usuarioId.ToString());
            if (!resultado.Ok)
            {
                TempData["Error"] = resultado.Mensaje;
                return RedirectToAction("Detalle", "SolicitudAOCR", new { id = codigoSolicitud });
            }
            TempData["Success"] = resultado.Existente
                ? "La solicitud institucional para esta NC ya existía; no se creó un duplicado."
                : "Nueva solicitud institucional creada y vinculada a la NC, inspección e informe originales.";
            if (string.Equals(resultado.ModuloDestino, "M5_SOLICITUD_INSPECCION_EMISION_RENOVACION", StringComparison.OrdinalIgnoreCase))
            {
                if (resultado.CodigoOrden.HasValue)
                    return RedirectToAction("Detalles", "OrdenRecaudacion", new { id = resultado.CodigoOrden.Value });
                return RedirectToAction("FormularioEmisionAOCR", "SolicitudAOCR", new { oid = resultado.CodigoSolicitud, tipoSolicitud = resultado.TipoTramite == "RENOVACION" ? 2 : 1 });
            }
            if (string.Equals(resultado.ModuloDestino, "M6_SOLICITUD_INSPECCION_MODIFICACION", StringComparison.OrdinalIgnoreCase) && resultado.CodigoInspeccion.HasValue)
                return RedirectToAction("Detalle", "Inspeccion", new { id = resultado.CodigoInspeccion.Value });
            TempData["Error"] = "La solicitud fue creada, pero no se pudo determinar el destino institucional.";
            return RedirectToAction("Index", "SolicitudAOCR", new { tipoSolicitud = 3 });
        }
        
        [HttpGet]
        [Authorize(Roles = ROL_RT + "," + ROL_RT_ALIAS + "," + ROL_ADMIN)]
        [Obsolete("Use SolicitudAOCR/Subsanar. Compatibilidad de navegación únicamente.")]
        public ActionResult SubsanarNc(int codigoSolicitud)
        {
            int usuarioId;
            if (!TryObtenerUsuarioActualId(out usuarioId))
                return RedirectToAction("Login", "Account");

            var solicitud = _solicitudDAO.ObtenerPorId(codigoSolicitud);
            if (solicitud == null) return HttpNotFound();
            if (!EsPropietarioSolicitud(solicitud,usuarioId))return new HttpStatusCodeResult(403,"No está autorizado para subsanar esta solicitud.");

            System.Diagnostics.Trace.TraceWarning("[GATE7A][LEGACY_REDIRECT] Endpoint=RT/SubsanarNc; SolicitudId={0}; UsuarioId={1}", codigoSolicitud, usuarioId);
            TempData["Warning"] = "La carga general fue retirada. Subsane individualmente cada documento observado.";
            return RedirectToAction("Subsanar", "SolicitudAOCR", new { id = codigoSolicitud });

#pragma warning disable 162
            // Código histórico conservado temporalmente solo como referencia de migración.
            // Verificar si hay NC SIN_INSPECCION
            var ncDao = new CapaDatos.DAOs.NoConformidadDAO();
            var ncs = ncDao.ListarPorSolicitud(codigoSolicitud);
            var ultimaNc = ncs.OrderByDescending(n => n.Version).FirstOrDefault();

            if (ultimaNc == null || ultimaNc.TipoRuta != "SIN_INSPECCION")
            {
                TempData["Error"] = "No existe una No Conformidad con ruta SIN INSPECCION pendiente de subsanar.";
                return RedirectToAction("Detalle", "SolicitudAOCR", new { id = codigoSolicitud });
            }

            if (ultimaNc.Estado != "FIRMADA_COORDINADOR" && ultimaNc.Estado != "EN_SUBSANACION")
            {
                TempData["Error"] = $"La No Conformidad actual está en estado {ultimaNc.Estado} y no admite subsanación en este momento.";
                return RedirectToAction("Detalle", "SolicitudAOCR", new { id = codigoSolicitud });
            }

            ViewBag.NumeroSolicitud = solicitud.NumeroSolicitud;
            return View(ultimaNc);
#pragma warning restore 162
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ROL_RT + "," + ROL_RT_ALIAS + "," + ROL_ADMIN)]
        [Obsolete("La carga general está deshabilitada. Use SolicitudAOCR/EnviarSubsanacionAlInspector.")]
        public ActionResult SubsanarNcPost(int codigoNoConformidad, System.Web.HttpPostedFileBase archivoSubsanacion)
        {
            int usuarioId;
            if (!TryObtenerUsuarioActualId(out usuarioId))
                return RedirectToAction("Login", "Account");

            var ncDao = new CapaDatos.DAOs.NoConformidadDAO();
            var nc = ncDao.ObtenerPorId(codigoNoConformidad);
            if (nc == null) return HttpNotFound();
            var solicitud=_solicitudDAO.ObtenerPorId(nc.CodigoSolicitud);
            if(!EsPropietarioSolicitud(solicitud,usuarioId))return new HttpStatusCodeResult(403,"No está autorizado para subsanar esta solicitud.");
            System.Diagnostics.Trace.TraceWarning("[GATE7A][LEGACY_WRITE_BLOCKED] Endpoint=RT/SubsanarNcPost; NC={0}; SolicitudId={1}; UsuarioId={2}; ArchivoIgnorado={3}", codigoNoConformidad, nc.CodigoSolicitud, usuarioId, archivoSubsanacion != null);
            TempData["Warning"] = "La carga general está obsoleta y fue rechazada. Use la subsanación individual por documento.";
            return RedirectToAction("Subsanar", "SolicitudAOCR", new { id = nc.CodigoSolicitud });

#pragma warning disable 162
            if(!string.Equals(nc.TipoRuta,"SIN_INSPECCION",StringComparison.OrdinalIgnoreCase)
                || (!string.Equals(nc.Estado,"FIRMADA_COORDINADOR",StringComparison.OrdinalIgnoreCase)&&!string.Equals(nc.Estado,"EN_SUBSANACION",StringComparison.OrdinalIgnoreCase)))
                return new HttpStatusCodeResult(409,"La No Conformidad no admite subsanación en su estado actual.");

            if (archivoSubsanacion != null && archivoSubsanacion.ContentLength > 0)
            {
                if(archivoSubsanacion.ContentLength>MAX_SUBSANACION_BYTES)return new HttpStatusCodeResult(400,"El PDF supera el máximo de 10 MB.");
                var header=new byte[5];archivoSubsanacion.InputStream.Position=0;var read=archivoSubsanacion.InputStream.Read(header,0,header.Length);archivoSubsanacion.InputStream.Position=0;
                if(read!=5||System.Text.Encoding.ASCII.GetString(header)!="%PDF-")return new HttpStatusCodeResult(400,"El contenido cargado no es un PDF válido.");
                var folder = Server.MapPath("~/App_Data/SubsanacionesNC/");
                if (!System.IO.Directory.Exists(folder))
                    System.IO.Directory.CreateDirectory(folder);

                string ext = System.IO.Path.GetExtension(archivoSubsanacion.FileName);
                if (string.IsNullOrEmpty(ext) || ext.ToLower() != ".pdf")
                {
                    TempData["Error"] = "El archivo debe ser un PDF válido.";
                    return RedirectToAction("SubsanarNc", new { codigoSolicitud = nc.CodigoSolicitud });
                }

                string fileName = $"NC_Subsanacion_{nc.CodigoSolicitud}_v{nc.Version}_{DateTime.Now.Ticks}.pdf";
                string path = System.IO.Path.Combine(folder, fileName);
                archivoSubsanacion.SaveAs(path);
                nc.RutaPdfSubsanacionRt = "~/App_Data/SubsanacionesNC/"+fileName;
            }
            else
            {
                TempData["Error"] = "Debe adjuntar un archivo PDF.";
                return RedirectToAction("SubsanarNc", new { codigoSolicitud = nc.CodigoSolicitud });
            }

            try
            {
                if(!ncDao.RegistrarSubsanacionRt(nc.CodigoNoConformidad,nc.RutaPdfSubsanacionRt,DateTime.Now))
                {var stale=Server.MapPath(nc.RutaPdfSubsanacionRt);if(System.IO.File.Exists(stale))System.IO.File.Delete(stale);return new HttpStatusCodeResult(409,"La No Conformidad cambió de estado antes de guardar la subsanación.");}
            }
            catch{var physical=Server.MapPath(nc.RutaPdfSubsanacionRt);if(System.IO.File.Exists(physical))System.IO.File.Delete(physical);throw;}

            try
            {
                if (!_solicitudDAO.CambiarEstado(nc.CodigoSolicitud, CapaDatos.Constants.EstadoSolicitud.Subsanada, usuarioId, "El RT ha subido la subsanación documental de la NC."))
                    throw new InvalidOperationException("No se pudo actualizar el estado de la solicitud.");
            }
            catch (Exception ex)
            {
                ncDao.ReabrirSubsanacionRt(nc.CodigoNoConformidad);
                var physical = Server.MapPath(nc.RutaPdfSubsanacionRt);
                if (System.IO.File.Exists(physical)) System.IO.File.Delete(physical);
                System.Diagnostics.Trace.TraceError("Error al completar subsanación NC {0}: {1}", nc.CodigoNoConformidad, ex);
                return new HttpStatusCodeResult(500, "No se pudo completar la subsanación. Intente nuevamente.");
            }

            TempData["Success"] = "La subsanación ha sido enviada exitosamente para revisión del Inspector.";
            return RedirectToAction("Detalle", "SolicitudAOCR", new { id = nc.CodigoSolicitud });
#pragma warning restore 162
        }

        [HttpGet]
        [Authorize(Roles = ROL_RT + "," + ROL_RT_ALIAS + "," + ROL_ADMIN)]
        public ActionResult DescargarNcRt(int codigoNoConformidad)
        {
            int usuarioId;if(!TryObtenerUsuarioActualId(out usuarioId))return new HttpStatusCodeResult(401);
            var nc=new NoConformidadDAO().ObtenerPorId(codigoNoConformidad);if(nc==null)return HttpNotFound();
            var solicitud=_solicitudDAO.ObtenerPorId(nc.CodigoSolicitud);
            if(!EsPropietarioSolicitud(solicitud,usuarioId))return new HttpStatusCodeResult(403);
            var estado=(nc.Estado??string.Empty).Trim().ToUpperInvariant();
            if(estado!="NOTIFICADA_RT"&&estado!="EN_SUBSANACION"&&estado!="SUBSANADA_RT"&&estado!="SUBSANACION_DEVUELTA"&&estado!="EN_REVISION_INSPECTOR"&&estado!="SUBSANACION_ACEPTADA"&&estado!="CERRADA")return new HttpStatusCodeResult(409);
            var ruta=nc.RutaPdfFirmadoCoordinador??nc.RutaPdfFirmadoInspector??nc.RutaPdf;
            var servicio=new CapaNegocio.Services.DocumentoSeguroService(new[]{Server.MapPath("~/App_Data")},
                evento=>System.Diagnostics.Trace.TraceInformation("[GATE7] "+evento+";UsuarioId="+usuarioId));
            var archivo=servicio.Resolver(nc.CodigoNoConformidad,nc.CodigoSolicitud,solicitud.CodigoSolicitud,ruta,
                "No_Conformidad_"+nc.CodigoNoConformidad+".pdf",Server.MapPath);
            if(!archivo.EsValido)return archivo.Error==CapaNegocio.Services.DocumentoSeguroError.NoEncontrado||archivo.Error==CapaNegocio.Services.DocumentoSeguroError.Vacio?(ActionResult)HttpNotFound(archivo.MensajePublico):new HttpStatusCodeResult(403,archivo.MensajePublico);
            return File(archivo.RutaFisica,archivo.Mime,archivo.NombreDescarga);
        }

        [HttpGet]
        [Authorize(Roles = ROL_RT + "," + ROL_RT_ALIAS + "," + ROL_ADMIN)]
        public ActionResult DescargarSubsanacionNc(int codigoNoConformidad)
        {
            int usuarioId;if(!TryObtenerUsuarioActualId(out usuarioId))return new HttpStatusCodeResult(401);
            var nc=new NoConformidadDAO().ObtenerPorId(codigoNoConformidad);if(nc==null)return HttpNotFound();
            if(!EsPropietarioSolicitud(_solicitudDAO.ObtenerPorId(nc.CodigoSolicitud),usuarioId))return new HttpStatusCodeResult(403);
            var seguro=new CapaNegocio.Services.DocumentoSeguroService(
                new[]{Path.GetFullPath(Server.MapPath("~/App_Data/SubsanacionesNC"))},
                evento=>System.Diagnostics.Trace.TraceInformation("[GATE7] "+evento+";UsuarioId="+usuarioId));
            var archivo=seguro.Resolver(nc.CodigoNoConformidad,nc.CodigoSolicitud,nc.CodigoSolicitud,
                nc.RutaPdfSubsanacionRt,"Subsanacion_NC_"+nc.CodigoNoConformidad+".pdf",Server.MapPath);
            if(!archivo.EsValido)return archivo.Error==CapaNegocio.Services.DocumentoSeguroError.NoEncontrado||archivo.Error==CapaNegocio.Services.DocumentoSeguroError.Vacio? (ActionResult)HttpNotFound(archivo.MensajePublico):new HttpStatusCodeResult(403,archivo.MensajePublico);
            return File(archivo.RutaFisica,archivo.Mime,archivo.NombreDescarga);
        }
    }
}
