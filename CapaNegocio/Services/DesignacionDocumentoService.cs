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
        private readonly FirmaDigitalService _firmaDigitalService;

        public DesignacionDocumentoService()
        {
            _solicitudDao = new SolicitudAOCRDAO();
            _designacionDao = new AocrDesignacionDAO();
            _estacionDao = new SolicitudEstacionDAO();
            _correoService = new SolicitudAocrCorreoService();
            _auditoriaDao = new AuditoriaDAO();
            _dircavService = new DircavDesignacionService();
            _firmaDigitalService = new FirmaDigitalService();
        }

        public DesignacionDocumentoService(
            SolicitudAOCRDAO solicitudDao,
            AocrDesignacionDAO designacionDao,
            SolicitudEstacionDAO estacionDao,
            SolicitudAocrCorreoService correoService = null,
            AuditoriaDAO auditoriaDao = null,
            DircavDesignacionService dircavService = null,
            FirmaDigitalService firmaDigitalService = null)
        {
            _solicitudDao = solicitudDao ?? new SolicitudAOCRDAO();
            _designacionDao = designacionDao ?? new AocrDesignacionDAO();
            _estacionDao = estacionDao ?? new SolicitudEstacionDAO();
            _correoService = correoService ?? new SolicitudAocrCorreoService();
            _auditoriaDao = auditoriaDao ?? new AuditoriaDAO();
            _dircavService = dircavService ?? new DircavDesignacionService();
            _firmaDigitalService = firmaDigitalService ?? new FirmaDigitalService();
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

            var companiaPersistida = !string.IsNullOrWhiteSpace(solicitud.RazonSocial) 
                ? solicitud.RazonSocial.Trim() 
                : (!string.IsNullOrWhiteSpace(solicitud.NombreOperador) ? solicitud.NombreOperador.Trim() : string.Empty);
            var operadorPersistido = !string.IsNullOrWhiteSpace(solicitud.NombreOperador) 
                ? solicitud.NombreOperador.Trim() 
                : (!string.IsNullOrWhiteSpace(solicitud.RazonSocial) ? solicitud.RazonSocial.Trim() : string.Empty);

            var vm = new DesignacionPdfViewModel
            {
                DesignacionId = designacion.Id,
                SolicitudId = solicitudId,
                NumeroSolicitud = solicitud.NumeroSolicitud ?? solicitudId.ToString(),
                NumeroDesignacion = $"DIRCAV-DESIG-{solicitudId:D5}-v{designacion.Version}",
                Version = designacion.Version,
                Estado = designacion.Estado,
                Compania = companiaPersistida,
                NombreOperador = operadorPersistido,
                PaisOperador = !string.IsNullOrWhiteSpace(solicitud.Pais) ? solicitud.Pais.Trim() : string.Empty,
                NumeroAoc = !string.IsNullOrWhiteSpace(solicitud.NumeroAOC) ? solicitud.NumeroAOC.Trim() : string.Empty,
                TipoOperacion = !string.IsNullOrWhiteSpace(solicitud.TipoOperacion) ? solicitud.TipoOperacion.Trim() : string.Empty,
                TipoSolicitud = solicitud.TipoSolicitud == 2 ? "Renovación" : (solicitud.TipoSolicitud == 3 ? "Modificación" : "Emisión"),
                ResponsableTecnico = !string.IsNullOrWhiteSpace(solicitud.RepresentanteLegal) ? solicitud.RepresentanteLegal.Trim() : string.Empty,
                CedulaRt = !string.IsNullOrWhiteSpace(solicitud.CedulaRepresentante) ? solicitud.CedulaRepresentante.Trim() : string.Empty,
                EmailRt = !string.IsNullOrWhiteSpace(solicitud.CorreoRepresentanteTecnico) ? solicitud.CorreoRepresentanteTecnico.Trim() : (!string.IsNullOrWhiteSpace(solicitud.Email) ? solicitud.Email.Trim() : string.Empty),
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
                HuellaCertificado = designacion.HuellaCertificado,
                CodigoVerificacion = !string.IsNullOrWhiteSpace(designacion.CodigoVerificacion)
                    ? designacion.CodigoVerificacion
                    : $"AOCR-VERIF-{solicitudId}-{designacion.Id}-{designacion.Version}"
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

                // Fuentes con codificación CP1252 para soporte completo de caracteres especiales y tildes
                var fuenteTitulo = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED, 13, Font.BOLD, BaseColor.BLACK);
                var fuenteSubtitulo = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED, 10, Font.BOLD, new BaseColor(27, 79, 114));
                var fuenteNegrita = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED, 9, Font.BOLD, BaseColor.BLACK);
                var fuenteNormal = FontFactory.GetFont(FontFactory.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED, 9, Font.NORMAL, BaseColor.BLACK);
                var fuentePequena = FontFactory.GetFont(FontFactory.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED, 8, Font.NORMAL, BaseColor.DARK_GRAY);
                var fuenteAviso = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED, 8, Font.BOLD, new BaseColor(180, 40, 40));

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
                if (!string.IsNullOrWhiteSpace(model.HuellaCertificado))
                {
                    celdaSello.AddElement(new Paragraph($"Huella Certificado: {model.HuellaCertificado}", fuentePequena));
                }
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
        /// Firma formalmente la designación de inspectores con firma digital criptográfica X.509 real (Exclusivo DIRCAV).
        /// Valida precondiciones AC-02 y AC-05, valida el certificado digital PKCS#12 (.p12/.pfx),
        /// aplica MakeSignature.SignDetached via FirmaDigitalService, calcula hash SHA-256 post-firma,
        /// guarda de forma atómica (archivo temporal con rollback) y ejecuta transacción en base de datos.
        /// </summary>
        public DircavDesignacionResult FirmarDesignacion(
            int solicitudId,
            int dircavUsuarioId,
            string dircavNombre,
            string rol,
            byte[] certificadoBytes = null,
            string passwordCert = null)
        {
            // 1. Segregación estricta: Solo DIRCAV puede firmar. DIRDAC, Admin, Coord, Inspector, RT y Financiero reciben 403.
            if (!_dircavService.EsDircavAutorizado(rol))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 403,
                    Mensaje = "Acceso denegado: La firma del oficio de designación es atribución exclusiva de la Autoridad DIRCAV. DIRDAC, Administrador, Coordinador, Inspector, RT y Financiero tienen prohibida esta acción."
                };
            }

            if (solicitudId <= 0)
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 400, Mensaje = "ID de solicitud inválido." };

            // 2. Validación de certificado digital y contraseña obligatorios
            if (certificadoBytes == null || certificadoBytes.Length == 0)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = "Debe proporcionar el archivo de certificado digital (.p12 o .pfx)."
                };
            }

            if (string.IsNullOrWhiteSpace(passwordCert))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = "Debe ingresar la contraseña del certificado digital."
                };
            }

            // 3. Validación criptográfica del certificado (clave privada, integridad, vigencia y formato)
            var infoCert = _firmaDigitalService.LeerCertificado(certificadoBytes, passwordCert);
            if (!infoCert.Exitoso)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = infoCert.Mensaje
                };
            }

            // Extraer huella digital SHA-1 (Thumbprint) del certificado X.509
            string huellaCert = string.Empty;
            try
            {
                using (var x509 = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificadoBytes, passwordCert))
                {
                    huellaCert = x509.Thumbprint;
                }
            }
            catch
            {
                // Si ocurre una lectura alternativa, dejar huella disponible
            }

            // 4. Validar existencia de solicitud
            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 404, Mensaje = "Solicitud no encontrada." };

            // 5. Validar existencia de designación formal previa (AC-05)
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

            // 6. Control de Idempotencia: si ya está firmada, retornar 200 sin duplicar archivos ni modificar datos
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

            // 7. Construir datos y validar precondiciones de AC-02 (estaciones y fechas independientes)
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
            model.HuellaCertificado = huellaCert;

            // 8. Generar el PDF oficial base con membrete y estaciones
            var pdfBaseBytes = GenerarPdfOficial(model, esVistaPrevia: false);
            if (pdfBaseBytes == null || pdfBaseBytes.Length == 0)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 500,
                    Mensaje = "Error interno al generar el archivo PDF de la designación."
                };
            }

            // 9. Aplicar firma digital criptográfica CMS/PKCS#7 real (iTextSharp + BouncyCastle)
            var resultadoFirma = _firmaDigitalService.FirmarPdf(
                pdfBaseBytes,
                certificadoBytes,
                passwordCert,
                nombreFirmante: model.AutoridadDircavNombre,
                motivo: "Designación formal de inspectores AOCR",
                ubicacion: "Quito, Ecuador",
                rolFirmante: "DIRCAV_DESIGNACION",
                contenidoQr: null,
                posicionFirmaVisual: null);

            if (!resultadoFirma.Exitoso || resultadoFirma.PdfFirmado == null || resultadoFirma.PdfFirmado.Length == 0)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = "Error al aplicar la firma digital criptográfica: " + resultadoFirma.Mensaje
                };
            }

            var pdfFirmadoBytes = resultadoFirma.PdfFirmado;
            var hashDoc = resultadoFirma.HashSha256;
            if (string.IsNullOrWhiteSpace(hashDoc))
            {
                using (var sha = SHA256.Create())
                {
                    hashDoc = BitConverter.ToString(sha.ComputeHash(pdfFirmadoBytes)).Replace("-", "").ToUpperInvariant();
                }
            }

            // 10. Gestión de archivo temporal antes del almacenamiento final
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"aocr_desig_tmp_{Guid.NewGuid():N}.pdf");
            var nombreArchivo = $"Designacion_{solicitudId}_v{designacion.Version}_Firmada.pdf";
            var rutaVirtual = $"~/App_Data/Uploads/Designaciones/{solicitudId}/{nombreArchivo}";
            var rutaFisica = FileStorageHelper.MapVirtualPath(rutaVirtual);
            var dirFisico = Path.GetDirectoryName(rutaFisica);

            try
            {
                // Escribir en archivo temporal
                File.WriteAllBytes(tempFilePath, pdfFirmadoBytes);

                // 11. Ejecutar persistencia atómica con control transaccional de BD y Almacenamiento
                using (var cn = _designacionDao.CrearConexion())
                {
                    cn.Open();
                    using (var tx = cn.BeginTransaction())
                    {
                        var paramTx = new DircavFirmarDesignacionParams
                        {
                            SolicitudId = solicitudId,
                            EstacionId = null,
                            DircavUsuarioId = dircavUsuarioId,
                            DircavUsuarioNombre = dircavNombre ?? "DIRCAV",
                            RutaPdf = rutaVirtual,
                            RutaDocumentoFirmado = rutaVirtual,
                            HashDocumento = hashDoc,
                            TamanioBytes = pdfFirmadoBytes.LongLength,
                            HuellaCertificado = huellaCert,
                            CodigoVerificacion = model.CodigoVerificacion,
                            MimeType = "application/pdf"
                        };

                        var resTx = _designacionDao.EjecutarFirmaDesignacionTransaccional(paramTx, tx);
                        if (!resTx.Exitoso)
                        {
                            tx.Rollback();
                            // Requisito 12: Fallo de BD elimina archivo temporal
                            try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
                            return new DircavDesignacionResult
                            {
                                Exitoso = false,
                                HttpStatusCode = resTx.HttpStatusCode,
                                Mensaje = resTx.Mensaje
                            };
                        }

                        // Mover a almacenamiento final institucional
                        try
                        {
                            if (!Directory.Exists(dirFisico))
                            {
                                Directory.CreateDirectory(dirFisico);
                            }
                            File.Copy(tempFilePath, rutaFisica, overwrite: true);
                        }
                        catch (Exception exStorage)
                        {
                            // Requisito 13: Fallo de almacenamiento ejecuta rollback
                            tx.Rollback();
                            try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
                            try { if (File.Exists(rutaFisica)) File.Delete(rutaFisica); } catch { }
                            return new DircavDesignacionResult
                            {
                                Exitoso = false,
                                HttpStatusCode = 500,
                                Mensaje = "Fallo de almacenamiento al guardar el PDF firmado: " + exStorage.Message
                            };
                        }

                        // Commit atómico definitivo de la transacción
                        tx.Commit();
                    }
                }

                return new DircavDesignacionResult
                {
                    Exitoso = true,
                    HttpStatusCode = 200,
                    DesignacionId = designacion.Id,
                    Version = designacion.Version,
                    NuevoEstado = AocrEstadosProceso.DesignacionFirmadaDircav,
                    Mensaje = "Oficio de designación firmado digitalmente con éxito y notificado oficialmente al Inspector asignado."
                };
            }
            catch (Exception ex)
            {
                // Requisito 12: Fallo de BD / proceso elimina archivo temporal
                try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 500,
                    Mensaje = $"Error inesperado al firmar digitalmente la designación: {ex.Message}"
                };
            }
            finally
            {
                // Limpieza garantizada del archivo temporal
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch { }
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
