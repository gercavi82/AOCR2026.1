using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaPresentacion.Models;
using CapaNegocio.Services;
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
            catch (Exception ex)
            {
                TempData["Error"] = "No se pudo cargar la bandeja de Validar AOCR: " + ex.Message;
                return RedirectToAction("RevisionVerificacion");
            }
        }

        [Authorize(Roles = "Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult DocumentoValidacionAocr(int solicitudId, string tipo, bool descargar = false)
        {
            var item = ConstruirItemValidacionAocr(_solicitudDao.ObtenerPorId(solicitudId), null);
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

            var documentoModel = ConstruirDocumentoPdfModel(item);
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

            var pdfBytes = pdf.BuildPdf(ControllerContext);
            return descargar
                ? File(pdfBytes, "application/pdf", nombreArchivo)
                : File(pdfBytes, "application/pdf");
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

            var informes = inspecciones
                .Select(inspeccion => new
                {
                    Inspeccion = inspeccion,
                    Informe = _informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion)
                })
                .Where(x => x.Informe != null)
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
                : AOCRPdfService.GenerarNumeroAOCR(solicitud.CodigoSolicitud, (contextoActivo != null ? contextoActivo.Informe.FechaFirma2 : null) ?? DateTime.Now);

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
                    UrlVer = item.FirmaCompleta ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "CONDICIONES_LIMITACIONES", descargar = false }) : null,
                    UrlDescargar = item.FirmaCompleta ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "CONDICIONES_LIMITACIONES", descargar = true }) : null,
                    FechaDocumento = fechaBase,
                    Disponible = item.FirmaCompleta
                }
            };
        }

        private AocrDocumentoPdfViewModel ConstruirDocumentoPdfModel(ValidarAocrSolicitudItemViewModel item)
        {
            return new AocrDocumentoPdfViewModel
            {
                Solicitud = item.Solicitud,
                Inspeccion = item.Inspeccion,
                Informe = item.Informe,
                Certificado = item.Certificado,
                Aeronaves = item.Aeronaves ?? new List<AeronaveSolicitud>(),
                NumeroAocr = item.NumeroAocr,
                FirmanteFinal = item.Informe != null ? item.Informe.UsuarioFirma2 : null,
                CargoFirmante = "Direccion General de Aviacion Civil",
                FechaEmisionDocumento = item.FechaFirmaFinal ?? item.FechaDisponibilidad ?? DateTime.Now
            };
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
    }
}