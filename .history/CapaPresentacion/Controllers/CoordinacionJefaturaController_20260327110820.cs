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
using CapaNegocio.Services;
using CapaPresentacion.Models;
using Npgsql;
using Rotativa;

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
        private readonly FirmaDigitalService _firmaDigitalService = new FirmaDigitalService();

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

            return View(model);
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
                    CustomSwitches = "--print-media-type --disable-smart-shrinking --margin-top 8mm --margin-bottom 8mm --margin-left 8mm --margin-right 8mm"
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
                    CustomSwitches = "--print-media-type --disable-smart-shrinking --margin-top 8mm --margin-bottom 8mm --margin-left 8mm --margin-right 8mm"
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