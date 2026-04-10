using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaNegocio.Services;
using CapaPresentacion.Models;
using Npgsql;
using Rotativa;
using LoggingServiceType = CapaDatos.Services.ILoggingService;
using LoggingFactoryType = CapaDatos.Services.LoggingServiceFactory;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
    public class CoordinacionJefaturaController : Controller
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();
        private readonly CertificadoDAO _certificadoDao = new CertificadoDAO();
        private readonly AeronaveSolicitudDAO _aeronaveSolicitudDao = new AeronaveSolicitudDAO();
        private readonly HistorialEstadoDAO _historialEstadoDao = new HistorialEstadoDAO();
        private readonly AocrFirmaDocumentoDAO _aocrFirmaDocumentoDao = new AocrFirmaDocumentoDAO();
        private readonly FirmaDigitalService _firmaDigitalService = new FirmaDigitalService();
        private readonly LoggingServiceType _logger = LoggingFactoryType.Create();

        [Authorize(Roles = "Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult DashboardGerencial()
        {
            return RedirectToAction("DashboardGerencial", "Direccion");
        }

        public ActionResult RevisionVerificacion()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            var inspecciones = _inspeccionDao.ListarTodas() ?? new List<Inspeccion>();

            var model = new CoordinacionJefaturaRevisionViewModel
            {
                SolicitudesControlDocumental = solicitudes
                    .Where(s =>
                    {
                        var estado = EstadoSolicitud.Normalizar(s.Estado);
                        return estado == EstadoSolicitud.Pendiente
                            || estado == EstadoSolicitud.EnRevision
                            || estado == EstadoSolicitud.Observada
                            || estado == EstadoSolicitud.AceptacionDocumental;
                    })
                    .OrderByDescending(s => s.FechaSolicitud ?? DateTime.MinValue)
                    .Take(30)
                    .ToList(),

                SolicitudesAocrRevision = solicitudes
                    .Where(s =>
                    {
                        var estado = EstadoSolicitud.Normalizar(s.Estado);
                        return estado == EstadoSolicitud.AOCR_EnElaboracion
                            || estado == EstadoSolicitud.AOCR_EnRevision
                            || estado == EstadoSolicitud.AOCR_Validado
                            || estado == EstadoSolicitud.AOCR_Legalizado;
                    })
                    .OrderByDescending(s => s.FechaSolicitud ?? DateTime.MinValue)
                    .Take(30)
                    .ToList(),

                InspeccionesSeguimiento = inspecciones
                    .Where(i =>
                    {
                        var estado = EstadosInspeccion.NormalizarEstado(i.Estado);
                        return EstadosInspeccion.EsEstadoBloqueCoordinacionJefatura(estado)
                            || string.Equals(estado, EstadosInspeccion.RESULTADO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(estado, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(estado, EstadosInspeccion.OBSERVACION_DOCUMENTAL, StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderByDescending(i => i.CodigoInspeccion)
                    .Take(30)
                    .ToList()
            };

            model.InspeccionesSeguimientoItems = ConstruirItemsSeguimientoInspeccion(model.InspeccionesSeguimiento);

            _logger.LogInfo(string.Format(
                "[CoordinacionJefatura] RevisionVerificacion cargada. ControlDocumental={0}, AocrRevision={1}, InspeccionesSeguimiento={2}, Usuario={3}",
                model.SolicitudesControlDocumental.Count,
                model.SolicitudesAocrRevision.Count,
                model.InspeccionesSeguimientoItems.Count,
                User != null && User.Identity != null ? User.Identity.Name : "anonimo"));

            if (!model.SolicitudesControlDocumental.Any() && !model.SolicitudesAocrRevision.Any() && !model.InspeccionesSeguimientoItems.Any())
            {
                _logger.LogWarning("[CoordinacionJefatura] RevisionVerificacion sin datos en las tres bandejas.");
            }

            return View(model);
        }

        [AllowAnonymous]
        public ActionResult PdfBrandingHeader()
        {
            return View("~/Views/Shared/_PdfBrandingHeader.cshtml");
        }

        [AllowAnonymous]
        public ActionResult PdfBrandingFooter()
        {
            return View("~/Views/Shared/_PdfBrandingFooter.cshtml");
        }

        private List<CoordinacionJefaturaInspeccionSeguimientoItemViewModel> ConstruirItemsSeguimientoInspeccion(IEnumerable<Inspeccion> inspecciones)
        {
            var items = new List<CoordinacionJefaturaInspeccionSeguimientoItemViewModel>();
            foreach (var inspeccion in inspecciones ?? Enumerable.Empty<Inspeccion>())
            {
                if (inspeccion == null)
                {
                    continue;
                }

                var solicitud = _solicitudDao.ObtenerPorId(inspeccion.CodigoSolicitud);
                var estadoNormalizado = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
                var estadosPermitidos = EstadosInspeccion.ObtenerEstadosPermitidos(estadoNormalizado);

                items.Add(new CoordinacionJefaturaInspeccionSeguimientoItemViewModel
                {
                    Inspeccion = inspeccion,
                    Solicitud = solicitud,
                    NumeroInspeccion = !string.IsNullOrWhiteSpace(inspeccion.NumeroInspeccion) ? inspeccion.NumeroInspeccion : inspeccion.CodigoInspeccion.ToString(),
                    NumeroSolicitud = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud) ? solicitud.NumeroSolicitud : inspeccion.CodigoSolicitud.ToString(),
                    OperadorNombre = ObtenerNombreOperadorSeguimiento(solicitud),
                    EstadoNormalizado = estadoNormalizado,
                    EtapaActual = EstadosInspeccion.ObtenerDescripcion(estadoNormalizado),
                    InspectorAsignado = ObtenerInspectorAsignadoSeguimiento(inspeccion, solicitud),
                    MensajeOperativo = ConstruirMensajeOperativoSeguimiento(estadoNormalizado, inspeccion),
                    ResumenAcciones = ConstruirResumenAccionesSeguimiento(estadosPermitidos, estadoNormalizado, inspeccion),
                    MensajeSinAcciones = ConstruirMensajeSinAccionesSeguimiento(estadoNormalizado, inspeccion),
                    PuedeAceptarSolicitud = estadosPermitidos.Any(x => string.Equals(EstadosInspeccion.NormalizarEstado(x), EstadosInspeccion.ACEPTADA, System.StringComparison.OrdinalIgnoreCase)),
                    PuedeObservar = estadosPermitidos.Any(x => string.Equals(EstadosInspeccion.NormalizarEstado(x), EstadosInspeccion.OBSERVADA, System.StringComparison.OrdinalIgnoreCase)),
                    PuedeCerrar = estadosPermitidos.Any(x => string.Equals(EstadosInspeccion.NormalizarEstado(x), EstadosInspeccion.CERRADA, System.StringComparison.OrdinalIgnoreCase)),
                    PuedeAsignarInspector = PuedeAsignarInspectorEnSeguimiento(estadoNormalizado, inspeccion)
                });
            }

            return items;
        }

        private static string ObtenerNombreOperadorSeguimiento(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return "No disponible";
            }

            if (!string.IsNullOrWhiteSpace(solicitud.RazonSocial))
            {
                return solicitud.RazonSocial.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.NombreOperador))
            {
                return solicitud.NombreOperador.Trim();
            }

            return "No disponible";
        }

        private static string ObtenerInspectorAsignadoSeguimiento(Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            if (inspeccion != null && !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre))
            {
                return inspeccion.InspectorPrincipalNombre.Trim();
            }

            if (solicitud != null && !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre))
            {
                return solicitud.TecnicoResponsableNombre.Trim();
            }

            return "No asignado";
        }

        private static bool PuedeAsignarInspectorEnSeguimiento(string estadoNormalizado, Inspeccion inspeccion)
        {
            if (inspeccion == null || inspeccion.CodigoInspector.HasValue)
            {
                return false;
            }

            return string.Equals(estadoNormalizado, EstadosInspeccion.ACEPTADA, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadosInspeccion.SUBSANADA, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadosInspeccion.PAGO_VALIDADO, System.StringComparison.OrdinalIgnoreCase);
        }

        private static string ConstruirMensajeOperativoSeguimiento(string estadoNormalizado, Inspeccion inspeccion)
        {
            if (string.Equals(estadoNormalizado, EstadosInspeccion.VERIFICACION_SOLICITUD, System.StringComparison.OrdinalIgnoreCase))
            {
                return "La solicitud se encuentra en verificacion por Direccion / Jefatura. Desde esta bandeja puede aceptar, observar o cerrar el tramite.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.ACEPTADA, System.StringComparison.OrdinalIgnoreCase))
            {
                return inspeccion != null && inspeccion.CodigoInspector.HasValue
                    ? "La solicitud fue aceptada y ya cuenta con inspector asignado. Puede continuar el seguimiento del avance."
                    : "La solicitud fue aceptada. El siguiente paso operativo es asignar inspector para iniciar la inspeccion.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.SUBSANADA, System.StringComparison.OrdinalIgnoreCase))
            {
                return "La compania subsano observaciones. Corresponde validar la informacion y definir si continua la inspeccion.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.EN_INSPECCION, System.StringComparison.OrdinalIgnoreCase))
            {
                return "La inspeccion se encuentra en ejecucion por el inspector asignado. Direccion / Jefatura puede revisar el avance y el contexto del tramite.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.INFORME_ELABORADO, System.StringComparison.OrdinalIgnoreCase))
            {
                return "El inspector ya elaboro el informe tecnico. Corresponde revisar resultados, observaciones y siguientes pasos del tramite.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.OBSERVADA, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadosInspeccion.OBSERVACION_DOCUMENTAL, System.StringComparison.OrdinalIgnoreCase))
            {
                return "El tramite mantiene observaciones pendientes. Revise el contexto y defina si procede subsanacion, continuidad o cierre.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.CERRADA, System.StringComparison.OrdinalIgnoreCase))
            {
                return "La inspeccion se encuentra cerrada y no tiene transiciones BPMN pendientes para esta bandeja.";
            }

            return "Revise el estado actual del tramite y ejecute solo acciones alineadas con la etapa operativa.";
        }

        private static string ConstruirResumenAccionesSeguimiento(IEnumerable<string> estadosPermitidos, string estadoNormalizado, Inspeccion inspeccion)
        {
            var acciones = new List<string>();
            foreach (var estado in estadosPermitidos ?? Enumerable.Empty<string>())
            {
                var destino = EstadosInspeccion.NormalizarEstado(estado);
                if (string.Equals(destino, EstadosInspeccion.ACEPTADA, StringComparison.OrdinalIgnoreCase))
                {
                    acciones.Add("Aceptar solicitud");
                }
                else if (string.Equals(destino, EstadosInspeccion.OBSERVADA, StringComparison.OrdinalIgnoreCase))
                {
                    acciones.Add("Observar");
                }
                else if (string.Equals(destino, EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase))
                {
                    acciones.Add("Cerrar");
                }
                else if (string.Equals(destino, EstadosInspeccion.SUBSANADA, StringComparison.OrdinalIgnoreCase))
                {
                    acciones.Add("Registrar subsanacion");
                }
            }

            if (PuedeAsignarInspectorEnSeguimiento(estadoNormalizado, inspeccion))
            {
                acciones.Add("Asignar inspector");
            }

            if (!acciones.Any())
            {
                return "Seguimiento informativo y trazabilidad del tramite.";
            }

            return "Acciones disponibles: " + string.Join(", ", acciones.Distinct());
        }

        private static string ConstruirMensajeSinAccionesSeguimiento(string estadoNormalizado, Inspeccion inspeccion)
        {
            if (string.Equals(estadoNormalizado, EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadosInspeccion.OBSERVACION_DOCUMENTAL, StringComparison.OrdinalIgnoreCase))
            {
                return "La ejecucion operativa sigue a cargo del inspector. Desde esta bandeja puede revisar el contexto y esperar el siguiente hito institucional.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase))
            {
                return "La inspeccion ya se encuentra cerrada y no registra transiciones BPMN pendientes para este rol.";
            }

            if (inspeccion != null && inspeccion.CodigoInspector.HasValue && string.Equals(estadoNormalizado, EstadosInspeccion.ACEPTADA, StringComparison.OrdinalIgnoreCase))
            {
                return "La solicitud ya fue aceptada y tiene inspector asignado. El siguiente avance visible dependera del trabajo operativo del inspector.";
            }

            return "No existe una transicion inmediata para este rol en la etapa actual. Utilice el detalle para revisar antecedentes y el contexto del tramite.";
        }

        [Authorize(Roles = "Direccion,JefaturaTecnica,Administrador")]
        public ActionResult AprobarSolicitudes()
        {
            return RedirectToAction("AprobarSolicitudes", "Direccion");
        }

        [Authorize(Roles = "Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult ValidarAocr()
        {
            try
            {
                var model = new ValidarAocrViewModel
                {
                    Items = ConstruirItemsValidacionAocr()
                };

                return View("~/Views/CoordinacionJefatura/ValidarAocr.cshtml", model);
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("ValidarAocr.CargarBandeja", exPg);
                TempData["Error"] = "No se pudo cargar la bandeja de Validar AOCR por un error de base de datos. Ref: " + referencia;
                return RedirectToAction("RevisionVerificacion");
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("ValidarAocr.CargarBandeja", ex);
                TempData["Error"] = "No se pudo cargar la bandeja de Validar AOCR. Ref: " + referencia;
                return RedirectToAction("RevisionVerificacion");
            }
        }

        [Authorize(Roles = "Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult DocumentoValidacionAocr(int solicitudId, string tipo, bool descargar = false)
        {
            try
            {
                var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
                if (solicitud == null)
                {
                    return HttpNotFound("La solicitud AOCR indicada no existe.");
                }

                var inspeccionesSolicitud = _inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>();
                var item = ConstruirItemValidacionAocr(solicitud, inspeccionesSolicitud);
                if (item == null)
                {
                    return HttpNotFound("No existe contexto disponible para el documento AOCR solicitado.");
                }

                if (!item.FirmaCompleta)
                {
                    return new HttpStatusCodeResult(409, "La firma del informe tecnico aun no esta completa para habilitar este documento.");
                }

                var tipoNormalizado = (tipo ?? string.Empty).Trim().ToUpperInvariant();
                if (tipoNormalizado != "RECONOCIMIENTO" && tipoNormalizado != "CONDICIONES_LIMITACIONES")
                {
                    return new HttpStatusCodeResult(400, "Tipo de documento AOCR no valido.");
                }

                RegistrarTrazabilidadDocumento(item.Solicitud, tipoNormalizado, descargar ? "DESCARGA" : "VISUALIZACION");

                if (tipoNormalizado == "RECONOCIMIENTO")
                {
                    var rutaExistente = item.Certificado != null ? item.Certificado.RutaDocumento : null;
                    var rutaFisica = ResolverRutaDocumento(rutaExistente);
                    if (!string.IsNullOrWhiteSpace(rutaFisica) && System.IO.File.Exists(rutaFisica))
                    {
                        var nombreArchivoExistente = Path.GetFileName(rutaFisica);
                        return descargar
                            ? File(rutaFisica, "application/pdf", string.IsNullOrWhiteSpace(nombreArchivoExistente) ? "Reconocimiento_AOCR.pdf" : nombreArchivoExistente)
                            : File(rutaFisica, "application/pdf");
                    }
                }

                var documentoModel = ConstruirDocumentoPdfModel(item, null, tipoNormalizado);
                var viewName = tipoNormalizado == "RECONOCIMIENTO"
                    ? "~/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml"
                    : "~/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml";
                var nombreArchivo = tipoNormalizado == "RECONOCIMIENTO"
                    ? "Reconocimiento_AOCR_" + item.Solicitud.CodigoSolicitud + ".pdf"
                    : "Condiciones_Limitaciones_AOCR_" + item.Solicitud.CodigoSolicitud + ".pdf";

                var pdf = new ViewAsPdf(viewName, documentoModel)
                {
                    FileName = nombreArchivo,
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    CustomSwitches = ConstruirSwitchesPdfValidacionAocr()
                };

                    var pdfBytes = pdf.BuildFile(ControllerContext);
                return descargar
                    ? File(pdfBytes, "application/pdf", nombreArchivo)
                    : File(pdfBytes, "application/pdf");
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("DocumentoValidacionAocr", exPg, solicitudId, null, tipo);
                return new HttpStatusCodeResult(500, "Error de base de datos al generar documento AOCR. Ref: " + referencia);
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("DocumentoValidacionAocr", ex, solicitudId, null, tipo);
                return new HttpStatusCodeResult(500, "Error interno al generar documento AOCR. Ref: " + referencia);
            }
        }

        [Authorize(Roles = "Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult EditarDocumentoValidacionAocr(int solicitudId, string tipo)
        {
            try
            {
                var tipoNormalizado = NormalizarTipoDocumento(tipo);
                if (tipoNormalizado == null)
                {
                    return new HttpStatusCodeResult(400, "Tipo de documento AOCR no valido.");
                }

                var item = ObtenerContextoDocumentoValidacion(solicitudId);
                if (item == null)
                {
                    return HttpNotFound("No existe contexto disponible para el documento AOCR solicitado.");
                }

                if (!item.FirmaCompleta)
                {
                    return new HttpStatusCodeResult(409, "La firma del informe tecnico aun no esta completa para habilitar este documento.");
                }

                var model = ConstruirDocumentoEdicionModel(item, tipoNormalizado);
                var viewName = tipoNormalizado == "RECONOCIMIENTO"
                    ? "~/Views/CoordinacionJefatura/EditarReconocimientoAocr.cshtml"
                    : "~/Views/CoordinacionJefatura/EditarCondicionesLimitacionesAocr.cshtml";

                return View(viewName, model);
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("EditarDocumentoValidacionAocr", exPg, solicitudId, null, tipo);
                return new HttpStatusCodeResult(500, "Error de base de datos al cargar la plantilla AOCR. Ref: " + referencia);
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("EditarDocumentoValidacionAocr", ex, solicitudId, null, tipo);
                return new HttpStatusCodeResult(500, "Error interno al cargar la plantilla AOCR. Ref: " + referencia);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public JsonResult CargarDatosFirmaDigitalAocr(HttpPostedFileBase certificadoDigital, string passwordCertificado)
        {
            string mensajeValidacion;
            if (!EsCertificadoDigitalValido(certificadoDigital, out mensajeValidacion))
            {
                Response.StatusCode = 400;
                return Json(new { ok = false, mensaje = mensajeValidacion });
            }

            if (string.IsNullOrWhiteSpace(passwordCertificado))
            {
                Response.StatusCode = 400;
                return Json(new { ok = false, mensaje = "Debe ingresar la contraseña del certificado digital." });
            }

            using (var ms = new MemoryStream())
            {
                certificadoDigital.InputStream.CopyTo(ms);
                var info = _firmaDigitalService.LeerCertificado(ms.ToArray(), passwordCertificado);
                if (!info.Exitoso)
                {
                    Response.StatusCode = 400;
                    return Json(new { ok = false, mensaje = info.Mensaje });
                }

                return Json(new
                {
                    ok = true,
                    nombreTitular = info.NombreTitular,
                    sujetoCertificado = info.SujetoCertificado,
                    vigenteDesde = info.VigenteDesde.HasValue ? info.VigenteDesde.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty,
                    vigenteHasta = info.VigenteHasta.HasValue ? info.VigenteHasta.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult GenerarDocumentoValidacionAocr(AocrDocumentoEdicionViewModel model, string accion = null, HttpPostedFileBase certificadoDigital = null, string passwordCertificado = null)
        {
            try
            {
                var tipoNormalizado = NormalizarTipoDocumento(model != null ? model.TipoDocumento : null);
                if (model == null || tipoNormalizado == null)
                {
                    return new HttpStatusCodeResult(400, "No se recibieron datos validos para generar el documento AOCR.");
                }

                var item = ObtenerContextoDocumentoValidacion(model.SolicitudId);
                if (item == null)
                {
                    return HttpNotFound("No existe contexto disponible para el documento AOCR solicitado.");
                }

                var documentoModel = ConstruirDocumentoPdfModel(item, model, tipoNormalizado);
                var viewName = tipoNormalizado == "RECONOCIMIENTO"
                    ? "~/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml"
                    : "~/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml";
                var nombreArchivo = tipoNormalizado == "RECONOCIMIENTO"
                    ? "Reconocimiento_AOCR_" + model.SolicitudId + ".pdf"
                    : "Condiciones_Limitaciones_AOCR_" + model.SolicitudId + ".pdf";
                var descargar = string.Equals(accion, "DESCARGAR", StringComparison.OrdinalIgnoreCase);
                var firmarDigitalmente = string.Equals(accion, "FIRMAR_DESCARGAR", StringComparison.OrdinalIgnoreCase);

                RegistrarTrazabilidadDocumento(item.Solicitud, tipoNormalizado, descargar ? "DESCARGA_DESDE_PLANTILLA" : "VISUALIZACION_DESDE_PLANTILLA");

                var pdf = new ViewAsPdf(viewName, documentoModel)
                {
                    FileName = nombreArchivo,
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    CustomSwitches = ConstruirSwitchesPdfValidacionAocr()
                };

                var pdfBytes = pdf.BuildFile(ControllerContext);
                if (firmarDigitalmente)
                {
                    string mensajeValidacion;
                    if (!EsCertificadoDigitalValido(certificadoDigital, out mensajeValidacion))
                    {
                        return new HttpStatusCodeResult(400, mensajeValidacion);
                    }

                    using (var ms = new MemoryStream())
                    {
                        certificadoDigital.InputStream.CopyTo(ms);
                        var infoCertificado = _firmaDigitalService.LeerCertificado(ms.ToArray(), passwordCertificado);
                        if (!infoCertificado.Exitoso)
                        {
                            return new HttpStatusCodeResult(400, infoCertificado.Mensaje);
                        }

                        var nombreFirmante = !string.IsNullOrWhiteSpace(model.FirmanteNombre)
                            ? model.FirmanteNombre
                            : infoCertificado.NombreTitular;
                        var motivoFirma = tipoNormalizado == "RECONOCIMIENTO"
                            ? "Firma digital del reconocimiento AOCR"
                            : "Firma digital del documento de condiciones y limitaciones AOCR";
                        var contenidoQr = ConstruirContenidoQrFirmaAocr(item, model, tipoNormalizado, infoCertificado, nombreFirmante);

                        var resultadoFirma = _firmaDigitalService.FirmarPdf(
                            pdfBytes,
                            ms.ToArray(),
                            passwordCertificado,
                            nombreFirmante,
                            motivoFirma,
                            "Sistema AOCR DGAC",
                            "AOCR_FIRMANTE",
                            contenidoQr);

                        if (!resultadoFirma.Exitoso)
                        {
                            return new HttpStatusCodeResult(400, resultadoFirma.Mensaje);
                        }

                        pdfBytes = resultadoFirma.PdfFirmado;
                        descargar = true;
                        nombreArchivo = Path.GetFileNameWithoutExtension(nombreArchivo) + "_firmado.pdf";

                        var rutaDocumentoFirmado = GuardarDocumentoFirmadoAocr(model.SolicitudId, tipoNormalizado, nombreArchivo, pdfBytes);
                        RegistrarFirmaDigitalAocr(
                            item,
                            model,
                            tipoNormalizado,
                            nombreArchivo,
                            rutaDocumentoFirmado,
                            resultadoFirma.HashSha256,
                            contenidoQr,
                            infoCertificado,
                            nombreFirmante);
                    }
                }

                return descargar
                    ? File(pdfBytes, "application/pdf", nombreArchivo)
                    : File(pdfBytes, "application/pdf");
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("GenerarDocumentoValidacionAocr", exPg, model != null ? (int?)model.SolicitudId : null, model != null ? model.InspeccionId : null, model != null ? model.TipoDocumento : null);
                return new HttpStatusCodeResult(500, "Error de base de datos al generar documento AOCR. Ref: " + referencia);
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("GenerarDocumentoValidacionAocr", ex, model != null ? (int?)model.SolicitudId : null, model != null ? model.InspeccionId : null, model != null ? model.TipoDocumento : null);
                return new HttpStatusCodeResult(500, "Error interno al generar documento AOCR. Ref: " + referencia);
            }
        }

        [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,DirectorGeneral,Administrador")]
        public ActionResult LegalizarAocr()
        {
            return RedirectToAction("RevisarLegalizacion", "SolicitudAOCR");
        }

        [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,DirectorGeneral,Administrador")]
        public ActionResult GenerarCertificados()
        {
            return RedirectToAction("GenerarCertificados", "CoordinacionLegal");
        }

        private List<ValidarAocrSolicitudItemViewModel> ConstruirItemsValidacionAocr()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            var inspecciones = _inspeccionDao.ListarTodas() ?? new List<Inspeccion>();
            var inspeccionesPorSolicitud = inspecciones
                .Where(i => i != null)
                .GroupBy(i => i.CodigoSolicitud)
                .ToDictionary(g => g.Key, g => g.ToList());

            return solicitudes
                .Select(solicitud => ConstruirItemValidacionAocr(
                    solicitud,
                    inspeccionesPorSolicitud.ContainsKey(solicitud.CodigoSolicitud)
                        ? inspeccionesPorSolicitud[solicitud.CodigoSolicitud]
                        : new List<Inspeccion>()))
                .Where(item => item != null)
                .OrderByDescending(item => item.FechaDisponibilidad ?? item.FechaFirmaFinal ?? item.Solicitud.FechaSolicitud ?? DateTime.MinValue)
                .ThenByDescending(item => item.Solicitud.CodigoSolicitud)
                .ToList();
        }

        private ValidarAocrSolicitudItemViewModel ConstruirItemValidacionAocr(SolicitudAOCR solicitud, IEnumerable<Inspeccion> inspeccionesSolicitud)
        {
            if (solicitud == null)
            {
                return null;
            }

            var estadoSolicitud = EstadoSolicitud.Normalizar(solicitud.Estado);
            var inspecciones = (inspeccionesSolicitud ?? new List<Inspeccion>())
                .Where(i => i != null)
                .ToList();

            var informes = new List<dynamic>();
            foreach (var inspeccion in inspecciones)
            {
                try
                {
                    var informe = _informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion);
                    if (informe != null)
                    {
                        informes.Add(new
                        {
                            Inspeccion = inspeccion,
                            Informe = informe
                        });
                    }
                }
                catch (Exception ex)
                {
                    RegistrarErrorValidacionAocr(
                        "ConstruirItemValidacionAocr.ObtenerUltimoPorInspeccion",
                        ex,
                        solicitud.CodigoSolicitud,
                        inspeccion.CodigoInspeccion);
                    throw;
                }
            }

            informes = informes
                .OrderByDescending(x => x.Informe.FechaFirma2 ?? x.Informe.FechaFirma1 ?? x.Informe.FechaFinalizacion ?? x.Informe.UpdatedAt ?? DateTime.MinValue)
                .ToList();

            var informeFirmado = informes
                .FirstOrDefault(x => x.Informe.Finalizado && x.Informe.FirmadoInspector && x.Informe.FirmadoDirdac);

            var estadoIncluido = string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, "ENVIADO_A_JEFATURA", StringComparison.OrdinalIgnoreCase);

            if (!estadoIncluido && informeFirmado == null)
            {
                return null;
            }

            var contextoActivo = informeFirmado ?? informes.FirstOrDefault();
            var firmaCompleta = informeFirmado != null;
            var certificado = _certificadoDao.ObtenerPorSolicitud(solicitud.CodigoSolicitud);
            var aeronaves = _aeronaveSolicitudDao.ObtenerPorSolicitud(solicitud.CodigoSolicitud) ?? new List<AeronaveSolicitud>();
            var numeroAocr = certificado != null && !string.IsNullOrWhiteSpace(certificado.NumeroCertificado)
                ? certificado.NumeroCertificado
                : GenerarNumeroAocr(solicitud.CodigoSolicitud, (contextoActivo != null ? contextoActivo.Informe.FechaFirma2 : null) ?? DateTime.Now);

            var item = new ValidarAocrSolicitudItemViewModel
            {
                Solicitud = solicitud,
                Inspeccion = contextoActivo != null ? contextoActivo.Inspeccion : null,
                Informe = contextoActivo != null ? contextoActivo.Informe : null,
                Certificado = certificado,
                Aeronaves = aeronaves,
                FirmaCompleta = firmaCompleta,
                NumeroAocr = numeroAocr,
                FechaFirmaFinal = contextoActivo != null ? contextoActivo.Informe.FechaFirma2 : null,
                FechaDisponibilidad = (contextoActivo != null ? contextoActivo.Informe.FechaFirma2 : null) ?? (certificado != null ? certificado.UpdatedAt : null) ?? solicitud.UpdatedAt,
                Firmantes = ConstruirFirmantes(contextoActivo != null ? contextoActivo.Informe : null),
                ListoParaEnvioRt = string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase)
            };

            item.Documentos = ConstruirDocumentosValidacion(item);
            item.PuedeContinuar = item.FirmaCompleta
                && item.Documentos.All(d => d.Disponible)
                && (string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, "ENVIADO_A_JEFATURA", StringComparison.OrdinalIgnoreCase));

            if (!item.FirmaCompleta)
            {
                item.MensajeEstado = "Pendiente de firma del informe tecnico";
                item.MensajeAdvertencia = "La firma institucional del informe tecnico aun no esta completa; por eso los documentos AOCR no se habilitan todavia.";
            }
            else if (item.Documentos.All(d => d.Disponible))
            {
                item.MensajeEstado = item.ListoParaEnvioRt
                    ? "Listo para envio al RT"
                    : "Documentos AOCR listos para validacion";
            }
            else
            {
                item.MensajeEstado = "Documentacion AOCR incompleta";
                item.MensajeAdvertencia = string.Join(" ", item.Documentos.Where(d => !d.Disponible).Select(d => d.Observacion).Where(x => !string.IsNullOrWhiteSpace(x)));
            }

            return item;
        }

        private List<ValidarAocrDocumentoItemViewModel> ConstruirDocumentosValidacion(ValidarAocrSolicitudItemViewModel item)
        {
            var urlHelper = new UrlHelper(ControllerContext.RequestContext);
            var fechaBase = item.FechaFirmaFinal ?? item.FechaDisponibilidad ?? DateTime.Now;

            return new List<ValidarAocrDocumentoItemViewModel>
            {
                new ValidarAocrDocumentoItemViewModel
                {
                    TipoDocumento = "RECONOCIMIENTO",
                    NombreVisible = "Reconocimiento de Certificado de Explotador de Servicios Aereos",
                    Estado = item.FirmaCompleta ? "Disponible" : "Pendiente",
                    Observacion = item.FirmaCompleta
                        ? "Documento listo para visualizacion, revision y descarga."
                        : "Falta firma final del informe tecnico para habilitar este documento.",
                    UrlEditar = item.FirmaCompleta ? urlHelper.Action("EditarDocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "RECONOCIMIENTO" }) : null,
                    UrlVer = item.FirmaCompleta ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "RECONOCIMIENTO", descargar = false }) : null,
                    UrlDescargar = item.FirmaCompleta ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "RECONOCIMIENTO", descargar = true }) : null,
                    FechaDocumento = item.Certificado != null ? (item.Certificado.UpdatedAt ?? item.Certificado.FechaEmision ?? fechaBase) : fechaBase,
                    Disponible = item.FirmaCompleta
                },
                new ValidarAocrDocumentoItemViewModel
                {
                    TipoDocumento = "CONDICIONES_LIMITACIONES",
                    NombreVisible = "Condiciones y Limitaciones",
                    Estado = item.FirmaCompleta ? "Disponible" : "Pendiente",
                    Observacion = item.FirmaCompleta
                        ? "Documento listo para visualizacion, revision y descarga."
                        : "Falta firma final del informe tecnico para habilitar este documento.",
                    UrlEditar = item.FirmaCompleta ? urlHelper.Action("EditarDocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "CONDICIONES_LIMITACIONES" }) : null,
                    UrlVer = item.FirmaCompleta ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "CONDICIONES_LIMITACIONES", descargar = false }) : null,
                    UrlDescargar = item.FirmaCompleta ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "CONDICIONES_LIMITACIONES", descargar = true }) : null,
                    FechaDocumento = fechaBase,
                    Disponible = item.FirmaCompleta
                }
            };
        }

        private AocrDocumentoPdfViewModel ConstruirDocumentoPdfModel(ValidarAocrSolicitudItemViewModel item, AocrDocumentoEdicionViewModel edicion, string tipoDocumento)
        {
            var firmanteFinal = item.Certificado != null && !string.IsNullOrWhiteSpace(item.Certificado.AprobadoPor)
                ? item.Certificado.AprobadoPor
                : item.Certificado != null && !string.IsNullOrWhiteSpace(item.Certificado.EmitidoPor)
                    ? item.Certificado.EmitidoPor
                    : item.Solicitud != null && !string.IsNullOrWhiteSpace(item.Solicitud.Director)
                        ? item.Solicitud.Director
                        : item.Informe != null
                            ? item.Informe.UsuarioFirma2
                            : null;

            var cargoFirmante = item.Solicitud != null && !string.IsNullOrWhiteSpace(item.Solicitud.CargoDirector)
                ? item.Solicitud.CargoDirector
                : "Direccion General de Aviacion Civil";

            var operador = item.Solicitud != null ? (item.Solicitud.RazonSocial ?? item.Solicitud.NombreOperador ?? item.Solicitud.NombreComercial) : null;
            var correoExplotador = item.Solicitud != null ? item.Solicitud.Email : null;
            var telefonoExplotador = item.Solicitud != null ? item.Solicitud.Telefono : null;
            var puntoContacto = item.Solicitud != null ? item.Solicitud.RepresentanteLegal : null;
            var contactoCorreo = item.Solicitud != null ? (item.Solicitud.CorreoRepresentanteTecnico ?? item.Solicitud.Email) : null;
            var restricciones = item.Certificado != null ? item.Certificado.Observaciones : (item.Informe != null ? item.Informe.Recomendaciones : null);
            var condicionBase = item.Solicitud != null
                ? string.Join(" / ", new[] { item.Solicitud.TipoOperacion, item.Solicitud.AeropuertosEcuador }.Where(x => !string.IsNullOrWhiteSpace(x)))
                : null;
            var aeronavesCondiciones = ConstruirFilasAeronavesCondiciones(item.Aeronaves);

            if (edicion != null && edicion.AeronavesCondiciones != null && edicion.AeronavesCondiciones.Any())
            {
                aeronavesCondiciones = edicion.AeronavesCondiciones;
            }

            return new AocrDocumentoPdfViewModel
            {
                Solicitud = item.Solicitud,
                Inspeccion = item.Inspeccion,
                Informe = item.Informe,
                Certificado = item.Certificado,
                Aeronaves = item.Aeronaves ?? new List<AeronaveSolicitud>(),
                NumeroAocr = item.NumeroAocr,
                FirmanteFinal = edicion != null && !string.IsNullOrWhiteSpace(edicion.FirmanteNombre) ? edicion.FirmanteNombre : firmanteFinal,
                CargoFirmante = edicion != null && !string.IsNullOrWhiteSpace(edicion.FirmanteCargo) ? edicion.FirmanteCargo : cargoFirmante,
                FechaEmisionDocumento = edicion != null ? edicion.FechaEmisionDocumento : item.FechaFirmaFinal ?? item.FechaDisponibilidad ?? DateTime.Now,
                FechaExpedicion = edicion != null ? edicion.FechaExpedicion : item.FechaFirmaFinal ?? item.FechaDisponibilidad,
                FechaRenovacion = edicion != null ? edicion.FechaRenovacion : null,
                FechaVencimiento = edicion != null ? edicion.FechaVencimiento : item.Certificado != null ? item.Certificado.FechaVencimiento : null,
                AocOriginalNumero = edicion != null ? edicion.AocOriginalNumero : item.Certificado != null ? item.Certificado.NumeroCertificado : item.NumeroAocr,
                EstadoOtorgante = edicion != null ? edicion.EstadoOtorgante : "Estado del Operador",
                NombreExplotador = edicion != null ? edicion.NombreExplotador : operador,
                EstadoExplotador = edicion != null ? edicion.EstadoExplotador : item.Solicitud != null ? item.Solicitud.Pais : null,
                RazonSocial = edicion != null ? edicion.RazonSocial : item.Solicitud != null ? item.Solicitud.RazonSocial : null,
                DireccionExplotador = edicion != null ? edicion.DireccionExplotador : item.Solicitud != null ? item.Solicitud.Direccion : null,
                TelefonoExplotador = edicion != null ? edicion.TelefonoExplotador : telefonoExplotador,
                CorreoExplotador = edicion != null ? edicion.CorreoExplotador : correoExplotador,
                PuntoContactoEcuador = edicion != null ? edicion.PuntoContactoEcuador : puntoContacto,
                ContactoDireccion = edicion != null ? edicion.ContactoDireccion : item.Solicitud != null ? item.Solicitud.Direccion : null,
                ContactoTelefono = edicion != null ? edicion.ContactoTelefono : telefonoExplotador,
                ContactoCorreo = edicion != null ? edicion.ContactoCorreo : contactoCorreo,
                PuntosContactoOperacionales = edicion != null ? edicion.PuntosContactoOperacionales : ConstruirPuntosContactoOperacionales(item.Solicitud),
                BaseLegalReferencia = edicion != null ? edicion.BaseLegalReferencia : item.Solicitud != null ? (item.Solicitud.AprobacionesEspecialesOtros ?? item.Solicitud.AprobacionesEspeciales ?? item.Solicitud.DescripcionOperacion) : null,
                ObservacionesReconocimiento = edicion != null ? edicion.ObservacionesReconocimiento : item.Certificado != null ? item.Certificado.Observaciones : item.Informe != null ? (item.Informe.Conclusiones ?? item.Informe.Observaciones) : null,
                RepresentanteTecnico = edicion != null ? edicion.RepresentanteTecnico : item.Solicitud != null ? (item.Solicitud.TecnicoResponsableNombre ?? item.Solicitud.RepresentanteLegal) : null,
                CondicionBaseOperacion = edicion != null ? edicion.CondicionBaseOperacion : condicionBase,
                RestriccionesCondiciones = edicion != null ? edicion.RestriccionesCondiciones : restricciones,
                CondicionesAdicionales = edicion != null ? edicion.CondicionesAdicionales : null,
                ObservacionesValidacionFinal = edicion != null ? edicion.ObservacionesValidacionFinal : null,
                ElaboradoPor = edicion != null ? edicion.ElaboradoPor : item.Certificado != null ? item.Certificado.EmitidoPor : null,
                RevisadoPor = edicion != null ? edicion.RevisadoPor : item.Informe != null ? item.Informe.UsuarioFirma2 : null,
                AeronavesCondiciones = aeronavesCondiciones
            };
        }

        private ValidarAocrSolicitudItemViewModel ObtenerContextoDocumentoValidacion(int solicitudId)
        {
            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
            {
                return null;
            }

            var inspeccionesSolicitud = _inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>();
            return ConstruirItemValidacionAocr(solicitud, inspeccionesSolicitud);
        }

        private AocrDocumentoEdicionViewModel ConstruirDocumentoEdicionModel(ValidarAocrSolicitudItemViewModel item, string tipoDocumento)
        {
            var pdfModel = ConstruirDocumentoPdfModel(item, null, tipoDocumento);
            return new AocrDocumentoEdicionViewModel
            {
                SolicitudId = item.Solicitud != null ? item.Solicitud.CodigoSolicitud : 0,
                InspeccionId = item.Inspeccion != null ? (int?)item.Inspeccion.CodigoInspeccion : null,
                TipoDocumento = tipoDocumento,
                NumeroAocr = pdfModel.NumeroAocr,
                NombreDocumento = tipoDocumento == "RECONOCIMIENTO" ? "Reconocimiento AOCR" : "Condiciones y Limitaciones",
                AocOriginalNumero = pdfModel.AocOriginalNumero,
                EstadoOtorgante = pdfModel.EstadoOtorgante,
                NombreExplotador = pdfModel.NombreExplotador,
                EstadoExplotador = pdfModel.EstadoExplotador,
                RazonSocial = pdfModel.RazonSocial,
                DireccionExplotador = pdfModel.DireccionExplotador,
                TelefonoExplotador = pdfModel.TelefonoExplotador,
                CorreoExplotador = pdfModel.CorreoExplotador,
                PuntoContactoEcuador = pdfModel.PuntoContactoEcuador,
                ContactoDireccion = pdfModel.ContactoDireccion,
                ContactoTelefono = pdfModel.ContactoTelefono,
                ContactoCorreo = pdfModel.ContactoCorreo,
                PuntosContactoOperacionales = pdfModel.PuntosContactoOperacionales,
                BaseLegalReferencia = pdfModel.BaseLegalReferencia,
                ObservacionesReconocimiento = pdfModel.ObservacionesReconocimiento,
                RepresentanteTecnico = pdfModel.RepresentanteTecnico,
                CondicionBaseOperacion = pdfModel.CondicionBaseOperacion,
                RestriccionesCondiciones = pdfModel.RestriccionesCondiciones,
                CondicionesAdicionales = pdfModel.CondicionesAdicionales,
                ObservacionesValidacionFinal = pdfModel.ObservacionesValidacionFinal,
                FechaEmisionDocumento = pdfModel.FechaEmisionDocumento,
                FechaExpedicion = pdfModel.FechaExpedicion,
                FechaRenovacion = pdfModel.FechaRenovacion,
                FechaVencimiento = pdfModel.FechaVencimiento,
                ElaboradoPor = pdfModel.ElaboradoPor,
                RevisadoPor = pdfModel.RevisadoPor,
                FirmanteNombre = pdfModel.FirmanteFinal,
                FirmanteCargo = pdfModel.CargoFirmante,
                AeronavesCondiciones = pdfModel.AeronavesCondiciones
            };
        }

        private static string NormalizarTipoDocumento(string tipo)
        {
            var tipoNormalizado = (tipo ?? string.Empty).Trim().ToUpperInvariant();
            return tipoNormalizado == "RECONOCIMIENTO" || tipoNormalizado == "CONDICIONES_LIMITACIONES"
                ? tipoNormalizado
                : null;
        }

        private string ConstruirSwitchesPdfValidacionAocr()
        {
            var switches = PdfBrandingHelper.StandardRotativaSwitches
                + " --disable-smart-shrinking --margin-top 30mm --margin-bottom 26mm --margin-left 8mm --margin-right 8mm --header-spacing 0 --footer-spacing 0";

            var headerHtmlPath = CrearArchivoBrandingTemporal(true);
            var footerHtmlPath = CrearArchivoBrandingTemporal(false);

            if (!string.IsNullOrWhiteSpace(headerHtmlPath))
            {
                switches += " --header-html \"" + ConvertirRutaFisicaAUrlArchivo(headerHtmlPath) + "\"";
            }

            if (!string.IsNullOrWhiteSpace(footerHtmlPath))
            {
                switches += " --footer-html \"" + ConvertirRutaFisicaAUrlArchivo(footerHtmlPath) + "\"";
            }

            return switches;
        }

        private string CrearArchivoBrandingTemporal(bool esHeader)
        {
            if (Server == null)
            {
                return null;
            }

            var carpetaTemporal = Server.MapPath("~/App_Data/Temp/PdfBranding");
            if (!Directory.Exists(carpetaTemporal))
            {
                Directory.CreateDirectory(carpetaTemporal);
            }

            var fileName = esHeader ? "aocr_header.html" : "aocr_footer.html";
            var htmlPath = Path.Combine(carpetaTemporal, fileName);
            var html = esHeader ? ConstruirHtmlHeaderHoja() : ConstruirHtmlFooterHoja();

            if (string.IsNullOrWhiteSpace(html))
            {
                html = ConstruirHtmlBrandingFallback(esHeader);
            }

            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            System.IO.File.WriteAllText(htmlPath, html, Encoding.UTF8);
            return htmlPath;
        }

        private string ConstruirHtmlHeaderHoja()
        {
            var barra = ObtenerFuenteBrandingHoja("barra.png");
            var escudo = ObtenerFuenteBrandingHoja("escudo.png");
            var dgca = ObtenerFuenteBrandingHoja("DGCA.png");

            if (string.IsNullOrWhiteSpace(barra) || string.IsNullOrWhiteSpace(escudo) || string.IsNullOrWhiteSpace(dgca))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />"
                + "<style>html,body{{margin:0;padding:0;width:194mm;height:26mm;background:transparent;overflow:hidden;}}"
                + ".header{{position:relative;width:194mm;height:26mm;}}"
                + ".barra{{position:absolute;top:0;right:0;width:129mm;height:3.2mm;}}"
                + ".escudo{{position:absolute;left:0;top:6.2mm;width:34mm;height:auto;}}"
                + ".dgca{{position:absolute;right:0;top:8.2mm;width:82mm;height:auto;}}</style>"
                + "</head><body><div class=\"header\">"
                + "<img class=\"barra\" src=\"{0}\" alt=\"\" />"
                + "<img class=\"escudo\" src=\"{1}\" alt=\"Escudo Republica del Ecuador\" />"
                + "<img class=\"dgca\" src=\"{2}\" alt=\"Direccion General de Aviacion Civil\" />"
                + "</div></body></html>",
                HttpUtility.HtmlAttributeEncode(barra),
                HttpUtility.HtmlAttributeEncode(escudo),
                HttpUtility.HtmlAttributeEncode(dgca));
        }

        private string ConstruirHtmlFooterHoja()
        {
            var barra = ObtenerFuenteBrandingHoja("barra.png");
            var direccion = ObtenerFuenteBrandingHoja("direccion.png");
            var nuevo = ObtenerFuenteBrandingHoja("nuevo.png");

            if (string.IsNullOrWhiteSpace(barra) || string.IsNullOrWhiteSpace(direccion) || string.IsNullOrWhiteSpace(nuevo))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />"
                + "<style>html,body{{margin:0;padding:0;width:194mm;height:26mm;background:transparent;overflow:hidden;}}"
                + ".footer{{position:relative;width:194mm;height:26mm;}}"
                + ".barra{{position:absolute;left:0;top:0;width:72mm;height:3.2mm;}}"
                + ".direccion{{position:absolute;left:0mm;top:7.2mm;width:64mm;height:auto;}}"
                + ".nuevo{{position:absolute;right:0;top:6.2mm;width:44mm;height:auto;}}</style>"
                + "</head><body><div class=\"footer\">"
                + "<img class=\"barra\" src=\"{0}\" alt=\"\" />"
                + "<img class=\"direccion\" src=\"{1}\" alt=\"Direccion DGAC\" />"
                + "<img class=\"nuevo\" src=\"{2}\" alt=\"El Nuevo Ecuador\" />"
                + "</div></body></html>",
                HttpUtility.HtmlAttributeEncode(barra),
                HttpUtility.HtmlAttributeEncode(direccion),
                HttpUtility.HtmlAttributeEncode(nuevo));
        }

        private string ConstruirHtmlBrandingFallback(bool esHeader)
        {
            var assets = PdfBrandingHelper.ResolveAssets(Server, "CoordinacionJefaturaController.CrearArchivoBrandingTemporal");
            var imageSrc = esHeader
                ? ObtenerFuenteBranding(assets != null ? assets.HeaderPhysicalPath : null, assets != null ? assets.HeaderDataUri : null)
                : ObtenerFuenteBranding(assets != null ? assets.FooterPhysicalPath : null, assets != null ? assets.FooterDataUri : null);

            if (string.IsNullOrWhiteSpace(imageSrc))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />"
                + "<style>html,body{{margin:0;padding:0;width:194mm;background:transparent;}}"
                + ".wrap{{width:100%;text-align:center;line-height:0;}}"
                + ".wrap img{{display:block;width:194mm;max-width:194mm;height:auto;margin:0 auto;}}</style>"
                + "</head><body><div class=\"wrap\"><img src=\"{0}\" alt=\"{1}\" /></div></body></html>",
                HttpUtility.HtmlAttributeEncode(imageSrc),
                esHeader ? "Header institucional DGAC" : "Footer institucional DGAC");
        }

        private string ObtenerFuenteBrandingHoja(string fileName)
        {
            var physicalPath = Server.MapPath("~/Content/assets/imganes/hoja/" + fileName);
            if (string.IsNullOrWhiteSpace(physicalPath) || !System.IO.File.Exists(physicalPath))
            {
                return null;
            }

            return ConvertirRutaFisicaAUrlArchivo(physicalPath);
        }

        private static string ObtenerFuenteBranding(string physicalPath, string dataUri)
        {
            if (!string.IsNullOrWhiteSpace(physicalPath) && System.IO.File.Exists(physicalPath))
            {
                return ConvertirRutaFisicaAUrlArchivo(physicalPath);
            }

            return dataUri;
        }

        private static string ConvertirRutaFisicaAUrlArchivo(string physicalPath)
        {
            if (string.IsNullOrWhiteSpace(physicalPath))
            {
                return null;
            }

            return "file:///" + physicalPath.Replace('\\', '/');
        }

        private static string ConstruirPuntosContactoOperacionales(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return null;
            }

            var bloques = new List<string>();
            if (!string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre) || !string.IsNullOrWhiteSpace(solicitud.CorreoRepresentanteTecnico))
            {
                bloques.Add("Representante Tecnico: " + (solicitud.TecnicoResponsableNombre ?? "Pendiente") + Environment.NewLine
                    + "Correo: " + (solicitud.CorreoRepresentanteTecnico ?? solicitud.Email ?? "Pendiente") + Environment.NewLine
                    + "Telefono: " + (solicitud.Telefono ?? "Pendiente"));
            }

            if (!string.IsNullOrWhiteSpace(solicitud.RepresentanteLegal))
            {
                bloques.Add("Representante Legal: " + solicitud.RepresentanteLegal + Environment.NewLine
                    + "Direccion: " + (solicitud.Direccion ?? "Pendiente") + Environment.NewLine
                    + "Telefono: " + (solicitud.Telefono ?? "Pendiente") + Environment.NewLine
                    + "Correo: " + (solicitud.Email ?? "Pendiente"));
            }

            return bloques.Count > 0 ? string.Join(Environment.NewLine + Environment.NewLine, bloques) : null;
        }

        private bool EsCertificadoDigitalValido(HttpPostedFileBase archivo, out string mensaje)
        {
            mensaje = string.Empty;
            if (archivo == null || archivo.ContentLength <= 0)
            {
                mensaje = "Debe cargar un certificado digital en formato .p12 o .pfx.";
                return false;
            }

            var extension = Path.GetExtension(archivo.FileName ?? string.Empty);
            if (!string.Equals(extension, ".p12", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".pfx", StringComparison.OrdinalIgnoreCase))
            {
                mensaje = "Solo se admiten certificados digitales .p12 o .pfx.";
                return false;
            }

            if (archivo.ContentLength > 5 * 1024 * 1024)
            {
                mensaje = "El certificado digital supera el tamaño máximo permitido.";
                return false;
            }

            return true;
        }

        private static string ConstruirContenidoQrFirmaAocr(ValidarAocrSolicitudItemViewModel item, AocrDocumentoEdicionViewModel model, string tipoDocumento, InformacionCertificadoDigital infoCertificado, string nombreFirmante)
        {
            var solicitudId = model != null ? model.SolicitudId : 0;
            var numeroSolicitud = item != null && item.Solicitud != null
                ? (item.Solicitud.NumeroSolicitud ?? item.Solicitud.CodigoSolicitud.ToString())
                : solicitudId.ToString();
            var numeroAocr = model != null ? model.NumeroAocr : item != null ? item.NumeroAocr : null;
            var fechaFirma = DateTime.Now;

            var partes = new List<string>
            {
                "Sistema=AOCR DGAC",
                "Documento=" + (tipoDocumento ?? string.Empty),
                "SolicitudId=" + solicitudId,
                "NumeroSolicitud=" + (numeroSolicitud ?? string.Empty),
                "NumeroAOCR=" + (numeroAocr ?? string.Empty),
                "Firmante=" + (nombreFirmante ?? string.Empty),
                "Cargo=" + (model != null ? model.FirmanteCargo : string.Empty),
                "FechaFirma=" + fechaFirma.ToString("yyyy-MM-dd HH:mm:ss"),
                "Certificado=" + (infoCertificado != null ? infoCertificado.SujetoCertificado : string.Empty),
                "VigenciaHasta=" + ((infoCertificado != null && infoCertificado.VigenteHasta.HasValue) ? infoCertificado.VigenteHasta.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty)
            };

            return string.Join(" | ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        private string GuardarDocumentoFirmadoAocr(int solicitudId, string tipoDocumento, string nombreArchivo, byte[] contenido)
        {
            var carpetaRelativa = "~/App_Data/Uploads/AOCR/Firmados/" + solicitudId;
            var carpetaAbsoluta = Server.MapPath(carpetaRelativa);
            if (!Directory.Exists(carpetaAbsoluta))
            {
                Directory.CreateDirectory(carpetaAbsoluta);
            }

            var prefijo = string.Equals(tipoDocumento, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase)
                ? "reconocimiento"
                : "condiciones_limitaciones";
            var nombreSeguro = prefijo + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + solicitudId + ".pdf";
            var rutaAbsoluta = Path.Combine(carpetaAbsoluta, nombreSeguro);
            System.IO.File.WriteAllBytes(rutaAbsoluta, contenido ?? new byte[0]);

            return VirtualPathUtility.ToAbsolute(carpetaRelativa.TrimStart('~') + "/" + nombreSeguro);
        }

        private void RegistrarFirmaDigitalAocr(
            ValidarAocrSolicitudItemViewModel item,
            AocrDocumentoEdicionViewModel model,
            string tipoDocumento,
            string nombreArchivo,
            string rutaDocumento,
            string hashDocumento,
            string codigoQr,
            InformacionCertificadoDigital infoCertificado,
            string nombreFirmante)
        {
            var firma = new AocrFirmaDocumento
            {
                CodigoSolicitud = model != null ? model.SolicitudId : 0,
                CodigoInspeccion = model != null ? model.InspeccionId : null,
                TipoDocumento = tipoDocumento,
                NumeroAocr = model != null ? model.NumeroAocr : item != null ? item.NumeroAocr : null,
                NombreArchivo = nombreArchivo,
                RutaDocumento = rutaDocumento,
                HashDocumento = hashDocumento,
                CodigoQr = codigoQr,
                SujetoCertificado = infoCertificado != null ? infoCertificado.SujetoCertificado : null,
                NombreFirmante = nombreFirmante,
                CargoFirmante = model != null ? model.FirmanteCargo : null,
                FechaFirma = DateTime.Now,
                CodigoUsuario = ObtenerUsuarioActualIdSeguro() > 0 ? (int?)ObtenerUsuarioActualIdSeguro() : null,
                UsuarioNombre = User != null && User.Identity != null ? User.Identity.Name : null
            };

            _aocrFirmaDocumentoDao.Registrar(firma);

            if (item != null && item.Solicitud != null)
            {
                var estadoActual = EstadoSolicitud.Normalizar(item.Solicitud.Estado);
                var observacion = "Firma digital AOCR registrada. Documento=" + tipoDocumento
                    + "; Archivo=" + (nombreArchivo ?? "N/D")
                    + "; Hash=" + (hashDocumento ?? "N/D")
                    + "; Ruta=" + (rutaDocumento ?? "N/D");
                var usuarioId = ObtenerUsuarioActualIdSeguro();
                if (usuarioId > 0)
                {
                    _historialEstadoDao.RegistrarCambio(item.Solicitud.CodigoSolicitud, estadoActual, estadoActual, usuarioId, observacion);
                }
            }
        }

        private static List<AocrCondicionAeronaveFilaViewModel> ConstruirFilasAeronavesCondiciones(IEnumerable<AeronaveSolicitud> aeronaves)
        {
            var filas = (aeronaves ?? new List<AeronaveSolicitud>())
                .Where(a => a != null)
                .Select(a => new AocrCondicionAeronaveFilaViewModel
                {
                    ModeloTipo = ((a.Marca ?? string.Empty) + " " + (a.Modelo ?? string.Empty)).Trim(),
                    Matricula = a.Matricula,
                    Serie = a.Serie,
                    Uio = string.Empty,
                    Gye = string.Empty,
                    Mec = string.Empty,
                    Ltx = string.Empty
                })
                .ToList();

            while (filas.Count < 4)
            {
                filas.Add(new AocrCondicionAeronaveFilaViewModel());
            }

            return filas;
        }

        private static string ConstruirFirmantes(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return "Pendiente";
            }

            var firmantes = new List<string>();
            if (!string.IsNullOrWhiteSpace(informe.UsuarioFirma1))
            {
                firmantes.Add("Inspector: " + informe.UsuarioFirma1);
            }

            if (!string.IsNullOrWhiteSpace(informe.UsuarioFirma2))
            {
                firmantes.Add("Direccion/Jefatura: " + informe.UsuarioFirma2);
            }

            return firmantes.Count > 0 ? string.Join(" | ", firmantes) : "Pendiente";
        }

        private static string GenerarNumeroAocr(int codigoSolicitud, DateTime fechaBase)
        {
            return string.Format("AOCR-{0}-{1:D4}", fechaBase.Year, codigoSolicitud);
        }

        private string ResolverRutaDocumento(string rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa))
            {
                return null;
            }

            var ruta = rutaRelativa.Trim();
            if (Path.IsPathRooted(ruta))
            {
                return ruta;
            }

            if (ruta.StartsWith("~"))
            {
                return Server.MapPath(ruta);
            }

            return Server.MapPath("~" + (ruta.StartsWith("/") ? ruta : "/" + ruta));
        }

        private void RegistrarTrazabilidadDocumento(SolicitudAOCR solicitud, string tipoDocumento, string accion)
        {
            if (solicitud == null)
            {
                return;
            }

            var usuarioId = ObtenerUsuarioActualIdSeguro();
            if (usuarioId <= 0)
            {
                return;
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado);
            var observacion = accion + " documento AOCR [" + tipoDocumento + "] desde Validar AOCR.";
            _historialEstadoDao.RegistrarCambio(solicitud.CodigoSolicitud, estadoActual, estadoActual, usuarioId, observacion);
        }

        private int ObtenerUsuarioActualIdSeguro()
        {
            int usuarioId;
            if (Session != null && Session["IdUsuario"] != null && int.TryParse(Session["IdUsuario"].ToString(), out usuarioId))
            {
                return usuarioId;
            }

            if (Session != null && Session["CodigoUsuario"] != null && int.TryParse(Session["CodigoUsuario"].ToString(), out usuarioId))
            {
                return usuarioId;
            }

            return 0;
        }

        private string RegistrarErrorValidacionAocr(string operacion, Exception ex, int? solicitudId = null, int? inspeccionId = null, string tipoDocumento = null)
        {
            var referencia = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
            var detalle = ConstruirDetalleExcepcion(ex);
            var mensaje = string.Format(
                "Validar AOCR fallo. Ref={0}; Operacion={1}; SolicitudId={2}; InspeccionId={3}; TipoDocumento={4}",
                referencia,
                operacion ?? "N/A",
                solicitudId.HasValue ? solicitudId.Value.ToString() : "N/A",
                inspeccionId.HasValue ? inspeccionId.Value.ToString() : "N/A",
                string.IsNullOrWhiteSpace(tipoDocumento) ? "N/A" : tipoDocumento);

            LogBL.RegistrarError(mensaje, detalle, "CoordinacionJefaturaController", ObtenerUsuarioActualIdSeguro());

            try
            {
                var logDir = Server.MapPath("~/App_Data/Logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                var logPath = Path.Combine(logDir, "ValidarAocrRuntime.log");
                System.IO.File.AppendAllText(
                    logPath,
                    string.Format(
                        "[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}{2}{2}",
                        DateTime.Now,
                        mensaje + Environment.NewLine + detalle,
                        Environment.NewLine));
            }
            catch
            {
                // No bloquear el flujo si el log local falla.
            }

            return referencia;
        }

        private static string ConstruirDetalleExcepcion(Exception ex)
        {
            if (ex == null)
            {
                return "Excepcion nula.";
            }

            var builder = new StringBuilder();
            var actual = ex;
            var profundidad = 0;

            while (actual != null)
            {
                builder.AppendLine(string.Format("Nivel {0}: {1}", profundidad, actual.GetType().FullName));
                builder.AppendLine("Mensaje: " + actual.Message);

                var exPg = actual as PostgresException;
                if (exPg != null)
                {
                    builder.AppendLine("SqlState: " + exPg.SqlState);
                    builder.AppendLine("MessageText: " + exPg.MessageText);
                    builder.AppendLine("Detail: " + exPg.Detail);
                    builder.AppendLine("Hint: " + exPg.Hint);
                    builder.AppendLine("Where: " + exPg.Where);
                    builder.AppendLine("SchemaName: " + exPg.SchemaName);
                    builder.AppendLine("TableName: " + exPg.TableName);
                    builder.AppendLine("ColumnName: " + exPg.ColumnName);
                    builder.AppendLine("ConstraintName: " + exPg.ConstraintName);
                    builder.AppendLine("Position: " + exPg.Position);
                    builder.AppendLine("Routine: " + exPg.Routine);
                    builder.AppendLine("File: " + exPg.File);
                    builder.AppendLine("Line: " + exPg.Line);
                }

                builder.AppendLine("StackTrace:");
                builder.AppendLine(actual.StackTrace ?? "(sin stacktrace)");
                builder.AppendLine();

                actual = actual.InnerException;
                profundidad++;
            }

            return builder.ToString();
        }
    }
}
