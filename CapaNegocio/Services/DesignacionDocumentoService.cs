using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using iTextSharp.text;
using iTextSharp.text.pdf;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaModelo.DTOs;
using CapaNegocio.Helpers;

namespace CapaNegocio.Services
{
    /// <summary>
    /// AC-06: Servicio orquestador para la generación y firma institucional exclusiva del
    /// PDF oficial de Designación de Inspectores por parte de la Autoridad DIRCAV.
    /// Incorpora las estaciones y fechas independientes de AC-02, valida precondiciones de AC-05,
    /// aplica inmutabilidad tras firma y garantiza segregación estricta (DIRDAC y Admin excluidos).
    /// </summary>
    public class DesignacionDocumentoService
    {
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly AocrDesignacionDAO _designacionDao;
        private readonly SolicitudEstacionDAO _estacionDao;
        private readonly SolicitudAocrCorreoService _correoService;
        private readonly AuditoriaDAO _auditoriaDao;
        private readonly DircavDesignacionService _dircavService;

        public DesignacionDocumentoService()
        {
            _solicitudDao = new SolicitudAOCRDAO();
            _designacionDao = new AocrDesignacionDAO();
            _estacionDao = new SolicitudEstacionDAO();
            _correoService = new SolicitudAocrCorreoService();
            _auditoriaDao = new AuditoriaDAO();
            _dircavService = new DircavDesignacionService();
        }

        public DesignacionDocumentoService(
            SolicitudAOCRDAO solicitudDao,
            AocrDesignacionDAO designacionDao,
            SolicitudEstacionDAO estacionDao,
            SolicitudAocrCorreoService correoService = null,
            AuditoriaDAO auditoriaDao = null,
            DircavDesignacionService dircavService = null)
        {
            _solicitudDao = solicitudDao ?? new SolicitudAOCRDAO();
            _designacionDao = designacionDao ?? new AocrDesignacionDAO();
            _estacionDao = estacionDao ?? new SolicitudEstacionDAO();
            _correoService = correoService ?? new SolicitudAocrCorreoService();
            _auditoriaDao = auditoriaDao ?? new AuditoriaDAO();
            _dircavService = dircavService ?? new DircavDesignacionService();
        }

        #region Precondiciones y Construcción de Datos

        /// <summary>
        /// Valida las precondiciones y construye el ViewModel tipado para la designación.
        /// </summary>
        public DesignacionPdfViewModel ConstruirDatosDesignacion(int solicitudId, int? estacionId = null)
        {
            if (solicitudId <= 0)
                throw new ArgumentException("ID de solicitud inválido.", nameof(solicitudId));

            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
                throw new KeyNotFoundException($"No se encontró la solicitud AOCR #{solicitudId}.");

            var designacion = _designacionDao.ObtenerDesignacionVigente(solicitudId, estacionId);
            if (designacion == null)
                throw new InvalidOperationException($"La solicitud #{solicitudId} no cuenta con una designación formal de inspectores registrada por DIRCAV (Precondición AC-05).");

            if (string.IsNullOrWhiteSpace(designacion.InspectorNombre))
                throw new InvalidOperationException("La designación no cuenta con un Inspector Principal válido asignado.");

            // Cargar estaciones y fechas independientes de AC-02
            var estaciones = _estacionDao.ListarPorSolicitud(solicitudId) ?? new List<SolicitudEstacionInspeccion>();

            // Validar que cada estación tenga fechas independientes válidas
            if (estaciones.Any())
            {
                foreach (var est in estaciones.Where(e => e.Activo))
                {
                    if (est.FechaInicio == default(DateTime))
                    {
                        throw new InvalidOperationException($"La estación '{est.EstacionCodigo ?? est.EstacionNombre}' carece de fecha inicial de inspección programada (Precondición AC-02).");
                    }
                    if (est.FechaFin == default(DateTime))
                    {
                        throw new InvalidOperationException($"La estación '{est.EstacionCodigo ?? est.EstacionNombre}' carece de fecha final de inspección programada (Precondición AC-02).");
                    }
                }
            }

            var vm = new DesignacionPdfViewModel
            {
                DesignacionId = designacion.Id,
                SolicitudId = solicitudId,
                NumeroSolicitud = solicitud.NumeroSolicitud ?? solicitudId.ToString(),
                NumeroDesignacion = $"DIRCAV-DESIG-{solicitudId:D5}-v{designacion.Version}",
                Version = designacion.Version,
                Estado = designacion.Estado,
                Compania = solicitud.RazonSocial ?? solicitud.NombreOperador ?? "Operador Aéreo Extranjero",
                NombreOperador = solicitud.NombreOperador ?? solicitud.RazonSocial,
                PaisOperador = !string.IsNullOrWhiteSpace(solicitud.Pais) ? solicitud.Pais : "Estado del Explotador",
                NumeroAoc = solicitud.NumeroAOC ?? "AOC-RDAC129",
                TipoOperacion = !string.IsNullOrWhiteSpace(solicitud.TipoOperacion) ? solicitud.TipoOperacion : "Transporte Aéreo Regular",
                TipoSolicitud = solicitud.TipoSolicitud == 2 ? "Renovación" : (solicitud.TipoSolicitud == 3 ? "Modificación" : "Emisión"),
                ResponsableTecnico = solicitud.RepresentanteLegal ?? "Responsable Técnico Designado",
                CedulaRt = solicitud.CedulaRepresentante ?? string.Empty,
                EmailRt = solicitud.CorreoRepresentanteTecnico ?? solicitud.Email ?? string.Empty,
                InspectorPrincipalNombre = designacion.InspectorNombre,
                InspectorPrincipalCedula = designacion.InspectorCedula,
                InspectorPrincipalCargo = "Inspector de Operaciones / Aeronavegabilidad",
                InspectorApoyoNombre = designacion.InspectorApoyoNombre,
                InspectorApoyoCedula = designacion.InspectorApoyoCedula,
                InspectorApoyoCargo = !string.IsNullOrWhiteSpace(designacion.InspectorApoyoNombre) ? "Inspector Asistente / Apoyo Técnico" : string.Empty,
                FechaEmision = designacion.FechaDesignacion,
                FechaFirma = designacion.FechaFirma,
                AutoridadDircavNombre = !string.IsNullOrWhiteSpace(designacion.DircavUsuarioNombre) ? designacion.DircavUsuarioNombre : "Director de Certificación Aeronáutica",
                AutoridadDircavCargo = "Director de Certificación Aeronáutica (DIRCAV) - DGAC",
                EsVistaPrevia = !designacion.Firmado,
                HashDocumento = designacion.HashDocumento,
                CodigoVerificacion = $"AOCR-VERIF-{solicitudId}-{designacion.Id}-{designacion.Version}"
            };

            foreach (var est in estaciones.Where(e => e.Activo))
            {
                vm.Estaciones.Add(new DesignacionEstacionItemDto
                {
                    EstacionId = est.Id,
                    CodigoOaci = est.EstacionCodigo,
                    NombreCiudad = est.EstacionNombre,
                    FechaInicio = est.FechaInicio,
                    FechaFin = est.FechaFin,
                    Estado = est.Estado ?? "PROGRAMADA"
                });
            }

            return vm;
        }

        #endregion

        #region Generación del Documento PDF (iTextSharp)

        /// <summary>
        /// Genera el contenido binario del PDF oficial de designación con membrete DGAC,
        /// tabla de estaciones independientes y sellado institucional.
        /// </summary>
        public byte[] GenerarPdfOficial(DesignacionPdfViewModel model, bool esVistaPrevia = false)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            using (var ms = new MemoryStream())
            {
                // Formato A4 con márgenes para membrete institucional
                var doc = new Document(PageSize.A4, 36f, 36f, 100f, 60f);
                var writer = PdfWriter.GetInstance(doc, ms);
                writer.CloseStream = false;

                // Evento de encabezado y pie institucional
                var server = HttpContext.Current != null ? HttpContext.Current.Server : null;
                writer.PageEvent = PdfBrandingHelper.CreateITextPageEvent(server, "DesignacionDocumentoService");

                doc.AddAuthor("Dirección General de Aviación Civil - DIRCAV");
                doc.AddCreator("Sistema AOCR - Dirección de Certificación Aeronáutica");
                doc.AddTitle($"Oficio de Designación - Solicitud #{model.NumeroSolicitud}");
                doc.Open();

                // Fuentes
                var fuenteTitulo = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 13, BaseColor.BLACK);
                var fuenteSubtitulo = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(27, 79, 114));
                var fuenteNegrita = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.BLACK);
                var fuenteNormal = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);
                var fuentePequena = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.DARK_GRAY);
                var fuenteAviso = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, new BaseColor(180, 40, 40));

                // 1. Título y Oficio
                var tablaEncabezado = new PdfPTable(2) { WidthPercentage = 100 };
                tablaEncabezado.SetWidths(new float[] { 65f, 35f });

                var celdaTitulo = new PdfPCell
                {
                    Border = Rectangle.NO_BORDER,
                    PaddingBottom = 6f
                };
                celdaTitulo.AddElement(new Paragraph("DIRECCIÓN DE CERTIFICACIÓN AERONÁUTICA", fuenteSubtitulo));
                celdaTitulo.AddElement(new Paragraph("OFICIO OFICIAL DE DESIGNACIÓN DE INSPECTORES", fuenteTitulo));
                celdaTitulo.AddElement(new Paragraph("Vigilancia de Explotadores de Servicios Aéreos Extranjeros (RDAC 129)", fuentePequena));
                tablaEncabezado.AddCell(celdaTitulo);

                var celdaOficio = new PdfPCell
                {
                    Border = Rectangle.BOX,
                    BorderColor = new BaseColor(200, 200, 200),
                    BackgroundColor = new BaseColor(248, 249, 250),
                    Padding = 6f,
                    HorizontalAlignment = Element.ALIGN_RIGHT
                };
                celdaOficio.AddElement(new Paragraph($"Oficio: {model.NumeroDesignacion}", fuenteNegrita));
                celdaOficio.AddElement(new Paragraph($"Trámite AOCR: #{model.NumeroSolicitud}", fuenteNormal));
                celdaOficio.AddElement(new Paragraph($"Fecha: {model.FechaEmision:dd/MM/yyyy}", fuenteNormal));
                celdaOficio.AddElement(new Paragraph($"Versión: v{model.Version}", fuentePequena));
                tablaEncabezado.AddCell(celdaOficio);

                doc.Add(tablaEncabezado);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 2. Información del Operador y Trámite
                var tablaOperador = new PdfPTable(4) { WidthPercentage = 100 };
                tablaOperador.SetWidths(new float[] { 22f, 28f, 22f, 28f });

                AgregarFilaCabeceraSeccion(tablaOperador, "1. INFORMACIÓN DEL OPERADOR Y SOLICITUD");
                AgregarParDatos(tablaOperador, "Operador Extranjero:", model.NombreOperador, fuenteNegrita, fuenteNormal);
                AgregarParDatos(tablaOperador, "Tipo Solicitud:", model.TipoSolicitud, fuenteNegrita, fuenteNormal);
                AgregarParDatos(tablaOperador, "Razón Social:", model.Compania, fuenteNegrita, fuenteNormal);
                AgregarParDatos(tablaOperador, "País del Explotador:", model.PaisOperador, fuenteNegrita, fuenteNormal);
                AgregarParDatos(tablaOperador, "N° AOC Origen:", model.NumeroAoc, fuenteNegrita, fuenteNormal);
                AgregarParDatos(tablaOperador, "Tipo Operación:", model.TipoOperacion, fuenteNegrita, fuenteNormal);
                AgregarParDatos(tablaOperador, "Responsable Técnico:", model.ResponsableTecnico, fuenteNegrita, fuenteNormal);
                AgregarParDatos(tablaOperador, "Identificación RT:", model.CedulaRt, fuenteNegrita, fuenteNormal);

                doc.Add(tablaOperador);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 3. Inspectores Designados por DIRCAV
                var tablaInspectores = new PdfPTable(4) { WidthPercentage = 100 };
                tablaInspectores.SetWidths(new float[] { 22f, 28f, 22f, 28f });

                AgregarFilaCabeceraSeccion(tablaInspectores, "2. EQUIPO INSPECTOR DESIGNADO (AUTORIDAD DIRCAV)");
                AgregarParDatos(tablaInspectores, "Inspector Principal:", model.InspectorPrincipalNombre, fuenteNegrita, fuenteNormal);
                AgregarParDatos(tablaInspectores, "Identificación / Cédula:", model.InspectorPrincipalCedula, fuenteNegrita, fuenteNormal);
                AgregarParDatos(tablaInspectores, "Cargo / Función:", model.InspectorPrincipalCargo, fuenteNegrita, fuenteNormal);
                AgregarParDatos(tablaInspectores, "Autoridad Designante:", "DIRCAV - DGAC", fuenteNegrita, fuenteNormal);

                if (!string.IsNullOrWhiteSpace(model.InspectorApoyoNombre))
                {
                    AgregarParDatos(tablaInspectores, "Inspector de Apoyo:", model.InspectorApoyoNombre, fuenteNegrita, fuenteNormal);
                    AgregarParDatos(tablaInspectores, "Identificación Apoyo:", model.InspectorApoyoCedula, fuenteNegrita, fuenteNormal);
                }

                doc.Add(tablaInspectores);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 4. Estaciones y Fechas Independientes (AC-02)
                var tablaEstaciones = new PdfPTable(4) { WidthPercentage = 100 };
                tablaEstaciones.SetWidths(new float[] { 20f, 35f, 25f, 20f });

                AgregarFilaCabeceraSeccion(tablaEstaciones, "3. ESTACIONES Y FECHAS DE INSPECCIÓN ASIGNADAS (AC-02)");

                tablaEstaciones.AddCell(CrearCeldaTabla("Código OACI/IATA", fuenteNegrita, true));
                tablaEstaciones.AddCell(CrearCeldaTabla("Estación / Aeropuerto", fuenteNegrita, true));
                tablaEstaciones.AddCell(CrearCeldaTabla("Fechas de Inspección", fuenteNegrita, true));
                tablaEstaciones.AddCell(CrearCeldaTabla("Estado", fuenteNegrita, true));

                if (model.Estaciones != null && model.Estaciones.Any())
                {
                    foreach (var est in model.Estaciones)
                    {
                        tablaEstaciones.AddCell(CrearCeldaTabla(est.CodigoOaci ?? "N/A", fuenteNegrita, false));
                        tablaEstaciones.AddCell(CrearCeldaTabla(est.NombreCiudad ?? "Estación", fuenteNormal, false));
                        tablaEstaciones.AddCell(CrearCeldaTabla($"{est.FechaInicio:dd/MM/yyyy} al {est.FechaFin:dd/MM/yyyy}", fuenteNormal, false));
                        tablaEstaciones.AddCell(CrearCeldaTabla(est.Estado ?? "PROGRAMADA", fuentePequena, false));
                    }
                }
                else
                {
                    var celdaVacia = new PdfPCell(new Phrase("Inspección en estación base principal según programación autorizada.", fuenteNormal))
                    {
                        Colspan = 4,
                        Padding = 5f
                    };
                    tablaEstaciones.AddCell(celdaVacia);
                }

                doc.Add(tablaEstaciones);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 5. Alcance e Instrucciones Técnicas
                var parrafoAlcance = new Paragraph();
                parrafoAlcance.Add(new Chunk("Alcance y Mandato Técnico: ", fuenteNegrita));
                parrafoAlcance.Add(new Chunk("En cumplimiento con la Regulación Técnica de Aviación Civil RDAC Parte 129, el equipo inspector designado queda legalmente facultado para ejecutar la verificación documental, operativa y de instalaciones en las estaciones y fechas arriba indicadas, debiendo emitir la correspondiente Lista de Verificación (LV) y el Informe Técnico motivado.", fuenteNormal));
                doc.Add(parrafoAlcance);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 6. Bloque de Firma Institucional DIRCAV
                var tablaFirma = new PdfPTable(2) { WidthPercentage = 100 };
                tablaFirma.SetWidths(new float[] { 50f, 50f });

                var celdaFirmaDircav = new PdfPCell
                {
                    Border = Rectangle.BOX,
                    BorderColor = new BaseColor(180, 180, 180),
                    BackgroundColor = new BaseColor(252, 252, 253),
                    Padding = 8f
                };

                if (esVistaPrevia || !model.FechaFirma.HasValue)
                {
                    celdaFirmaDircav.AddElement(new Paragraph("FIRMA INSTITUCIONAL PENDIENTE", fuenteAviso));
                    celdaFirmaDircav.AddElement(new Paragraph("Documento en fase de revisión previa por DIRCAV.", fuentePequena));
                    celdaFirmaDircav.AddElement(new Paragraph($"Autoridad: {model.AutoridadDircavNombre}", fuenteNormal));
                    celdaFirmaDircav.AddElement(new Paragraph(model.AutoridadDircavCargo, fuentePequena));
                }
                else
                {
                    celdaFirmaDircav.AddElement(new Paragraph("FIRMADO ELECTRÓNICAMENTE POR:", fuenteSubtitulo));
                    celdaFirmaDircav.AddElement(new Paragraph(model.AutoridadDircavNombre.ToUpperInvariant(), fuenteTitulo));
                    celdaFirmaDircav.AddElement(new Paragraph(model.AutoridadDircavCargo, fuenteNegrita));
                    celdaFirmaDircav.AddElement(new Paragraph($"Fecha de Firma: {model.FechaFirma:dd/MM/yyyy HH:mm:ss} UTC-5", fuenteNormal));
                    celdaFirmaDircav.AddElement(new Paragraph("Dirección de Certificación Aeronáutica - DGAC Ecuador", fuentePequena));
                }
                tablaFirma.AddCell(celdaFirmaDircav);

                var celdaSello = new PdfPCell
                {
                    Border = Rectangle.BOX,
                    BorderColor = new BaseColor(180, 180, 180),
                    BackgroundColor = new BaseColor(248, 249, 250),
                    Padding = 8f
                };
                celdaSello.AddElement(new Paragraph("CONTROL DE INTEGRIDAD Y VERIFICACIÓN", fuenteSubtitulo));
                celdaSello.AddElement(new Paragraph($"Código: {model.CodigoVerificacion}", fuentePequena));
                if (!string.IsNullOrWhiteSpace(model.HashDocumento))
                {
                    celdaSello.AddElement(new Paragraph($"Hash SHA-256: {model.HashDocumento.Substring(0, Math.Min(32, model.HashDocumento.Length))}...", fuentePequena));
                }
                celdaSello.AddElement(new Paragraph("La validez de esta designación puede ser verificada en el expediente oficial del Sistema AOCR.", fuentePequena));
                tablaFirma.AddCell(celdaSello);

                doc.Add(tablaFirma);

                // Marca de agua si es vista previa
                if (esVistaPrevia)
                {
                    var cb = writer.DirectContentUnder;
                    cb.BeginText();
                    cb.SetFontAndSize(BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1252, false), 42);
                    cb.SetColorFill(new BaseColor(220, 220, 220));
                    cb.ShowTextAligned(Element.ALIGN_CENTER, "VISTA PREVIA NO OFICIAL", doc.PageSize.Width / 2, doc.PageSize.Height / 2, 45);
                    cb.EndText();
                }

                doc.Close();
                return ms.ToArray();
            }
        }

        #endregion

        #region Operaciones de Negocio: Vista Previa, Firma y Descarga

        /// <summary>
        /// Genera el byte array del PDF de vista previa para revisión de DIRCAV antes de firmar.
        /// </summary>
        public byte[] GenerarVistaPrevia(int solicitudId, int usuarioId, string rol)
        {
            if (!_dircavService.EsDircavAutorizado(rol) && !AocrRolesInstitucionales.EsCoordinador(rol))
            {
                throw new UnauthorizedAccessException("Acceso denegado: Solo DIRCAV o Coordinación pueden acceder a la vista previa de la designación.");
            }

            var model = ConstruirDatosDesignacion(solicitudId);
            model.EsVistaPrevia = true;
            return GenerarPdfOficial(model, esVistaPrevia: true);
        }

        /// <summary>
        /// Firma formalmente la designación de inspectores (Exclusivo DIRCAV).
        /// Garantiza idempotencia, genera el PDF firmado, calcula hash SHA-256,
        /// guarda en almacenamiento protegido, actualiza BD y encola notificación al inspector.
        /// </summary>
        public DircavDesignacionResult FirmarDesignacion(
            int solicitudId,
            int dircavUsuarioId,
            string dircavNombre,
            string rol,
            byte[] certificadoBytes = null,
            string passwordCert = null)
        {
            // 1. Segregación estricta: Solo DIRCAV puede firmar
            if (!_dircavService.EsDircavAutorizado(rol))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 403,
                    Mensaje = "Acceso denegado: La firma del oficio de designación es atribución exclusiva de la Autoridad DIRCAV. DIRDAC, Administrador y Coordinador tienen prohibida esta acción."
                };
            }

            if (solicitudId <= 0)
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 400, Mensaje = "ID de solicitud inválido." };

            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 404, Mensaje = "Solicitud no encontrada." };

            var designacion = _designacionDao.ObtenerDesignacionVigente(solicitudId);
            if (designacion == null)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 409,
                    Mensaje = "No existe una designación activa registrada para esta solicitud."
                };
            }

            // 2. Control de Idempotencia: si ya está firmada, retornar el resultado confirmado sin duplicar
            if (designacion.Firmado)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = true,
                    HttpStatusCode = 200,
                    DesignacionId = designacion.Id,
                    Version = designacion.Version,
                    NuevoEstado = AocrEstadosProceso.DesignacionFirmadaDircav,
                    Mensaje = "El oficio de designación ya se encontraba firmado formalmente por DIRCAV."
                };
            }

            // 3. Construir datos y validar precondiciones de AC-02 y AC-05
            DesignacionPdfViewModel model;
            try
            {
                model = ConstruirDatosDesignacion(solicitudId);
            }
            catch (Exception ex)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = $"No se puede firmar la designación: {ex.Message}"
                };
            }

            model.EsVistaPrevia = false;
            model.FechaFirma = DateTime.Now;
            model.AutoridadDircavNombre = !string.IsNullOrWhiteSpace(dircavNombre) ? dircavNombre : "Autoridad DIRCAV";

            // 4. Generar el PDF oficial
            var pdfBytes = GenerarPdfOficial(model, esVistaPrevia: false);
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 500,
                    Mensaje = "Error interno al generar el archivo PDF de la designación."
                };
            }

            // 5. Calcular Hash SHA-256
            string hashDoc;
            using (var sha = SHA256.Create())
            {
                hashDoc = BitConverter.ToString(sha.ComputeHash(pdfBytes)).Replace("-", "").ToUpperInvariant();
            }

            // 6. Almacenamiento seguro bajo ~/App_Data/Uploads/Designaciones/{solicitudId}/
            var nombreArchivo = $"Designacion_{solicitudId}_v{designacion.Version}_Firmada.pdf";
            var rutaVirtual = $"~/App_Data/Uploads/Designaciones/{solicitudId}/{nombreArchivo}";
            var rutaFisica = FileStorageHelper.MapVirtualPath(rutaVirtual);
            var dirFisico = Path.GetDirectoryName(rutaFisica);

            try
            {
                if (!Directory.Exists(dirFisico))
                {
                    Directory.CreateDirectory(dirFisico);
                }
                File.WriteAllBytes(rutaFisica, pdfBytes);
            }
            catch (Exception exStorage)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 500,
                    Mensaje = $"Error al almacenar el archivo PDF firmado: {exStorage.Message}"
                };
            }

            // 7. Persistencia en Base de Datos
            try
            {
                _designacionDao.MarcarFirmada(
                    designacion.Id,
                    rutaVirtual,
                    hashDoc,
                    dircavNombre ?? "DIRCAV",
                    DateTime.Now,
                    pdfBytes.LongLength
                );

                solicitud.Estado = AocrEstadosProceso.DesignacionFirmadaDircav;
                solicitud.UpdatedAt = DateTime.Now;
                _solicitudDao.Actualizar(solicitud);

                // Auditoría
                try
                {
                    _auditoriaDao.Registrar(new Auditoria
                    {
                        Entidad = "DIRCAV",
                        Accion = "FIRMAR_DESIGNACION_INSPECTOR",
                        Usuario = dircavNombre ?? "DIRCAV",
                        Fecha = DateTime.Now,
                        DatosPrevios = AocrEstadosProceso.DesignacionPendienteFirmaDircav,
                        DatosNuevos = $"{AocrEstadosProceso.DesignacionFirmadaDircav} Oficio:{model.NumeroDesignacion} Hash:{hashDoc}"
                    });
                }
                catch { }

                // 8. Encolar correo al Inspector asignado post-commit
                try
                {
                    _correoService.NotificarEvento(solicitud, "DESIGNACION_FIRMADA_INSPECTOR", $"Oficio {model.NumeroDesignacion} emitido por DIRCAV.");
                }
                catch { }

                return new DircavDesignacionResult
                {
                    Exitoso = true,
                    HttpStatusCode = 200,
                    DesignacionId = designacion.Id,
                    Version = designacion.Version,
                    NuevoEstado = AocrEstadosProceso.DesignacionFirmadaDircav,
                    Mensaje = "Oficio de designación firmado y notificado oficialmente al Inspector asignado."
                };
            }
            catch (Exception exDb)
            {
                // Rollback de archivo si la base de datos falla
                try
                {
                    if (File.Exists(rutaFisica)) File.Delete(rutaFisica);
                }
                catch { }

                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 500,
                    Mensaje = $"Error de base de datos al registrar la firma de la designación: {exDb.Message}"
                };
            }
        }

        /// <summary>
        /// Obtiene los datos del PDF firmado para descarga autorizada.
        /// Valida que el solicitante sea DIRCAV, Coordinador o el Inspector asignado.
        /// Inspectores ajenos o usuarios no autorizados reciben 403 Forbidden.
        /// </summary>
        public byte[] ObtenerDocumentoParaDescarga(int solicitudId, int usuarioId, string rol, string usuarioLogin, out string nombreDescarga)
        {
            nombreDescarga = string.Empty;

            var designacion = _designacionDao.ObtenerDesignacionVigente(solicitudId);
            if (designacion == null || !designacion.Firmado)
            {
                throw new FileNotFoundException("El oficio de designación aún no ha sido firmado por DIRCAV.");
            }

            // Validación de Autorización para descarga:
            // 1. DIRCAV y Coordinador siempre pueden descargar
            // 2. Si es Inspector, DEBE ser el Inspector asignado
            var esDircav = _dircavService.EsDircavAutorizado(rol);
            var esCoord = AocrRolesInstitucionales.EsCoordinador(rol);
            var esInspector = AocrRolesInstitucionales.EsInspector(rol);

            if (esInspector)
            {
                var cedulaLogin = usuarioLogin ?? string.Empty;
                var esAsignado = (usuarioId > 0 && usuarioId == designacion.InspectorId)
                    || (!string.IsNullOrWhiteSpace(designacion.InspectorCedula) && string.Equals(designacion.InspectorCedula, cedulaLogin, StringComparison.OrdinalIgnoreCase));

                if (!esAsignado)
                {
                    throw new UnauthorizedAccessException("Acceso denegado (403): Solo el Inspector asignado al expediente puede descargar este oficio de designación.");
                }
            }
            else if (!esDircav && !esCoord)
            {
                throw new UnauthorizedAccessException("Acceso denegado (403): No tiene autorización para descargar este documento institucional.");
            }

            nombreDescarga = $"Oficio_Designacion_AOCR_{solicitudId}_v{designacion.Version}.pdf";

            // Si existe en disco, servirlo
            if (!string.IsNullOrWhiteSpace(designacion.RutaDocumentoFirmado))
            {
                var rutaFisica = FileStorageHelper.MapVirtualPath(designacion.RutaDocumentoFirmado);
                if (File.Exists(rutaFisica))
                {
                    return File.ReadAllBytes(rutaFisica);
                }
            }

            // Fallback: regenerar con los mismos metadatos históricos
            var model = ConstruirDatosDesignacion(solicitudId);
            model.EsVistaPrevia = false;
            model.FechaFirma = designacion.FechaFirma ?? designacion.FechaDesignacion;
            return GenerarPdfOficial(model, esVistaPrevia: false);
        }

        #endregion

        #region Helpers de Tabla PDF

        private static void AgregarFilaCabeceraSeccion(PdfPTable tabla, string titulo)
        {
            var celda = new PdfPCell(new Phrase(titulo, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.WHITE)))
            {
                Colspan = tabla.NumberOfColumns,
                BackgroundColor = new BaseColor(27, 79, 114),
                Padding = 4f,
                HorizontalAlignment = Element.ALIGN_LEFT
            };
            tabla.AddCell(celda);
        }

        private static void AgregarParDatos(PdfPTable tabla, string etiqueta, string valor, Font fuenteEtiqueta, Font fuenteValor)
        {
            var celdaEt = new PdfPCell(new Phrase(etiqueta, fuenteEtiqueta))
            {
                BackgroundColor = new BaseColor(245, 247, 250),
                BorderColor = new BaseColor(220, 224, 230),
                Padding = 4f
            };
            var celdaVal = new PdfPCell(new Phrase(valor ?? string.Empty, fuenteValor))
            {
                BorderColor = new BaseColor(220, 224, 230),
                Padding = 4f
            };
            tabla.AddCell(celdaEt);
            tabla.AddCell(celdaVal);
        }

        private static PdfPCell CrearCeldaTabla(string texto, Font fuente, bool esCabecera)
        {
            return new PdfPCell(new Phrase(texto ?? string.Empty, fuente))
            {
                BackgroundColor = esCabecera ? new BaseColor(235, 240, 245) : BaseColor.WHITE,
                BorderColor = new BaseColor(210, 215, 220),
                Padding = 4f,
                HorizontalAlignment = Element.ALIGN_LEFT
            };
        }

        #endregion
    }
}
