using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaModelo.DTOs;
using CapaNegocio.Helpers;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace CapaNegocio.Services
{
    /// <summary>
    /// AC-10: Servicio centralizado para la generación, validación, revisión, devolución,
    /// remisión, firma institucional, versionamiento y descarga de Condiciones y Limitaciones (CL).
    /// Segregación estricta de roles:
    /// - INSPECTOR: construye y remite el borrador con datos reales.
    /// - COORDINADOR: revisa, devuelve al Inspector con motivo o remite a DIRCAV.
    /// - DIRCAV: revisa, devuelve a Coordinación y firma exclusivamente CL.
    /// - DIRDAC: revisa y firma exclusivamente el AOCR (nunca genera ni firma CL -> HTTP 403).
    /// - ADMINISTRADOR: no opera ni firma CL -> HTTP 403.
    /// - RT: descarga solo tras la finalización institucional con ambas firmas.
    /// </summary>
    public class CondicionesLimitacionesService
    {
        private readonly CondicionesLimitacionesDAO _clDao;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly SolicitudEstacionDAO _estacionDao;
        private readonly AeronaveSolicitudDAO _aeronaveDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly InspeccionInformeDAO _informeDao;
        private readonly ListaVerificacionOperacionalEaeDAO _lvDao;
        private readonly AocrDocumentoGeneradoDAO _documentoGeneradoDao;
        private readonly AocrFirmaDocumentoDAO _firmaDao;

        public CondicionesLimitacionesService()
        {
            _clDao = new CondicionesLimitacionesDAO();
            _solicitudDao = new SolicitudAOCRDAO();
            _estacionDao = new SolicitudEstacionDAO();
            _aeronaveDao = new AeronaveSolicitudDAO();
            _inspeccionDao = new InspeccionDAO();
            _informeDao = new InspeccionInformeDAO();
            _lvDao = new ListaVerificacionOperacionalEaeDAO();
            _documentoGeneradoDao = new AocrDocumentoGeneradoDAO();
            _firmaDao = new AocrFirmaDocumentoDAO();
        }

        public CondicionesLimitacionesService(
            CondicionesLimitacionesDAO clDao,
            SolicitudAOCRDAO solicitudDao,
            SolicitudEstacionDAO estacionDao,
            AeronaveSolicitudDAO aeronaveDao,
            InspeccionDAO inspeccionDao,
            InspeccionInformeDAO informeDao,
            ListaVerificacionOperacionalEaeDAO lvDao,
            AocrDocumentoGeneradoDAO documentoGeneradoDao = null,
            AocrFirmaDocumentoDAO firmaDao = null)
        {
            _clDao = clDao ?? new CondicionesLimitacionesDAO();
            _solicitudDao = solicitudDao ?? new SolicitudAOCRDAO();
            _estacionDao = estacionDao ?? new SolicitudEstacionDAO();
            _aeronaveDao = aeronaveDao ?? new AeronaveSolicitudDAO();
            _inspeccionDao = inspeccionDao ?? new InspeccionDAO();
            _informeDao = informeDao ?? new InspeccionInformeDAO();
            _lvDao = lvDao ?? new ListaVerificacionOperacionalEaeDAO();
            _documentoGeneradoDao = documentoGeneradoDao ?? new AocrDocumentoGeneradoDAO();
            _firmaDao = firmaDao ?? new AocrFirmaDocumentoDAO();
        }

        #region 1. Precondiciones y Construcción de Datos

        /// <summary>
        /// Valida precondiciones rigurosas de AC-02, AC-07/AC-08 y AC-09 antes de permitir la generación de CL.
        /// </summary>
        public void ValidarPrecondicionesGeneracion(int solicitudId)
        {
            if (solicitudId <= 0)
                throw new ArgumentException("El ID de solicitud es inválido.", nameof(solicitudId));

            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
                throw new KeyNotFoundException($"No se encontró la solicitud AOCR #{solicitudId}.");

            // 1. Validar estaciones autorizadas con fechas independientes (AC-02)
            var estaciones = _estacionDao.ListarPorSolicitud(solicitudId) ?? new List<SolicitudEstacionInspeccion>();
            if (!estaciones.Any(e => e.Activo))
            {
                throw new InvalidOperationException("La solicitud no cuenta con estaciones autorizadas configuradas (Precondición AC-02).");
            }

            foreach (var est in estaciones.Where(e => e.Activo))
            {
                if (est.FechaInicio == default(DateTime) || est.FechaFin == default(DateTime))
                {
                    throw new InvalidOperationException($"La estación '{est.EstacionCodigo ?? est.EstacionNombre}' carece de fechas de inspección válidas (Precondición AC-02).");
                }
            }

            // 2. Validar que exista al menos una inspección
            var inspecciones = _inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>();
            if (!inspecciones.Any())
            {
                throw new InvalidOperationException("La solicitud no cuenta con inspecciones asociadas para sustentar las Condiciones y Limitaciones.");
            }

            var inspeccionPrincipal = inspecciones.OrderByDescending(i => i.CodigoInspeccion).First();

            // 3. Validar Lista de Verificación (AC-07 / AC-08)
            var lv = _lvDao.ObtenerUltimaPorInspeccion(inspeccionPrincipal.CodigoInspeccion);
            if (lv == null || !lv.FirmadoTecnico)
            {
                throw new InvalidOperationException("La Lista de Verificación (LV) debe estar completa y debidamente firmada por el Inspector (Precondición AC-07 / AC-08).");
            }

            // 4. Validar Informe Técnico (AC-09)
            var informe = _informeDao.ObtenerUltimoPorInspeccion(inspeccionPrincipal.CodigoInspeccion);
            if (informe == null || !informe.FirmadoInspector)
            {
                throw new InvalidOperationException("El Informe Técnico debe estar finalizado y firmado por el Inspector (Precondición AC-09).");
            }

            if (!EsInformeAprobado(informe.EstadoInforme))
            {
                throw new InvalidOperationException("El Informe Técnico debe estar formalmente aprobado antes de redactar las Condiciones y Limitaciones (Precondición AC-09).");
            }
        }

        private static bool EsInformeAprobado(string estado)
        {
            var e = (estado ?? string.Empty).Trim().ToUpperInvariant();
            return e == "APROBADO_DIRECCION"
                || e == "INFORME_TECNICO_APROBADO_DIRDAC"
                || e == "INFORME_TECNICO_APROBADO_DCAV"
                || e == AocrEstadosProceso.InformeTecnicoAprobadoDirdac
                || e == AocrEstadosProceso.InformeTecnicoAprobadoDcav;
        }

        /// <summary>
        /// Obtiene o construye el ViewModel tipado del expediente para Condiciones y Limitaciones.
        /// </summary>
        public CondicionesLimitacionesViewModel ObtenerOConstruirViewModel(int solicitudId, int usuarioId, string rol)
        {
            ValidarRolLectura(rol);

            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
                throw new KeyNotFoundException($"No se encontró la solicitud AOCR #{solicitudId}.");

            var clExistente = _clDao.ObtenerPorSolicitudVigente(solicitudId);
            var estaciones = _estacionDao.ListarPorSolicitud(solicitudId) ?? new List<SolicitudEstacionInspeccion>();
            var aeronaves = _aeronaveDao.ObtenerPorSolicitud(solicitudId) ?? new List<AeronaveSolicitud>();
            var inspecciones = _inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>();
            var inspeccion = inspecciones.OrderByDescending(i => i.CodigoInspeccion).FirstOrDefault();
            var informe = inspeccion != null ? _informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion) : null;

            // Verificar si AOCR está firmado por DIRDAC
            var aocrDoc = _documentoGeneradoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
            var aocrFirmado = aocrDoc != null && (string.Equals(aocrDoc.Estado, AocrEstadosProceso.AocrFirmadoDirdac, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(aocrDoc.HashPdfFirmado));

            var vm = new CondicionesLimitacionesViewModel
            {
                SolicitudId = solicitudId,
                InspeccionId = inspeccion != null ? inspeccion.CodigoInspeccion : (int?)null,
                InformeId = informe != null ? informe.CodigoInforme : (int?)null,
                NumeroSolicitud = solicitud.NumeroSolicitud ?? solicitud.CodigoSolicitud.ToString(),
                NumeroAocr = solicitud.NumeroAOC ?? (!string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud) ? "AOCR-" + solicitud.NumeroSolicitud : "AOCR-" + solicitudId),
                Compania = solicitud.RazonSocial ?? solicitud.NombreOperador ?? "Operador Aéreo",
                OperadorExtranjero = solicitud.NombreOperador ?? solicitud.RazonSocial ?? "Operador Aéreo",
                RepresentanteTecnico = solicitud.RepresentanteLegal ?? "Representante Técnico",
                PaisOperador = solicitud.Pais ?? "Ecuador",
                NumeroAoc = solicitud.NumeroAOC ?? "AOC-" + solicitudId,
                TipoOperacion = solicitud.TipoOperacion ?? "TRANSPORTE AÉREO REGULAR / NO REGULAR",
                Estaciones = estaciones.Where(e => e.Activo).ToList(),
                Aeronaves = aeronaves,
                InspectorNombre = informe != null && !string.IsNullOrWhiteSpace(informe.UsuarioFirma1) ? informe.UsuarioFirma1 : (inspeccion != null ? (inspeccion.InspectorPrincipalNombre ?? "Inspector Asignado") : "Inspector Asignado"),
                FechaInformeTecnico = informe != null ? (informe.FechaFirma1 ?? informe.CreatedAt) : null,
                EstadoAocr = aocrDoc != null ? aocrDoc.Estado : "PENDIENTE",
                AocrFirmadoDirdac = aocrFirmado
            };

            if (clExistente != null)
            {
                vm.Id = clExistente.Id;
                vm.Version = clExistente.Version;
                vm.Estado = clExistente.Estado;
                vm.Vigente = clExistente.Vigente;
                vm.CondicionesAprobadas = clExistente.CondicionesAprobadas;
                vm.Limitaciones = clExistente.Limitaciones;
                vm.Observaciones = clExistente.Observaciones;
                vm.RutasAutorizadas = clExistente.RutasAutorizadas;
                vm.AlcanceAutorizado = clExistente.AlcanceAutorizado;
                vm.ObservacionCoordinador = clExistente.ObservacionCoordinador;
                vm.FechaRevisionCoordinador = clExistente.FechaRevisionCoordinador;
                vm.CoordinadorNombre = clExistente.CoordinadorNombre;
                vm.ObservacionDircav = clExistente.ObservacionDircav;
                vm.DircavNombre = clExistente.DircavNombre;
                vm.FechaFirmaDircav = clExistente.FechaFirmaDircav;
                vm.HashPdf = clExistente.HashPdf;
                vm.HashPdfFirmado = clExistente.HashPdfFirmado;
                vm.CodigoVerificacion = clExistente.CodigoVerificacion;
                vm.RutaPdf = clExistente.RutaPdfFirmado ?? clExistente.RutaPdfBorrador;
                vm.RutaPdfFirmado = clExistente.RutaPdfFirmado;
                vm.TamanioPdf = clExistente.TamanioPdf;
            }
            else
            {
                vm.Estado = AocrEstadoCl.ClNoGenerada;
                vm.CondicionesAprobadas = ConstruirCondicionesAprobadasPorDefecto(solicitud, estaciones, aeronaves);
                vm.Limitaciones = ConstruirLimitacionesPorDefecto(solicitud, estaciones, aeronaves);
                vm.Observaciones = informe != null ? informe.Observaciones : string.Empty;
                vm.RutasAutorizadas = "Rutas internacionales y nacionales aprobadas por la Autoridad Aeronáutica.";
                vm.AlcanceAutorizado = "Operaciones de transporte comercial de pasajeros, carga y correo.";
            }

            // Evaluar permisos de acción según rol actual
            ConfigurarPermisosUI(vm, rol, usuarioId);

            return vm;
        }

        private static string ConstruirCondicionesAprobadasPorDefecto(SolicitudAOCR sol, List<SolicitudEstacionInspeccion> estaciones, List<AeronaveSolicitud> aeronaves)
        {
            var sb = new StringBuilder();
            sb.AppendLine("1. Operar en estricto apego a las Especificaciones Técnicas y Operacionales autorizadas en la RDAC 129.");
            if (estaciones.Any())
            {
                sb.AppendLine("2. Operaciones autorizadas en las siguientes estaciones verificadas: " + string.Join(", ", estaciones.Select(e => e.EstacionCodigo ?? e.EstacionNombre)));
            }
            if (aeronaves.Any())
            {
                sb.AppendLine("3. Equipos autorizados conforme a la flota inspeccionada: " + string.Join(", ", aeronaves.Select(a => $"{a.Marca} {a.Modelo} ({a.Matricula})")));
            }
            return sb.ToString().Trim();
        }

        private static string ConstruirLimitacionesPorDefecto(SolicitudAOCR sol, List<SolicitudEstacionInspeccion> estaciones, List<AeronaveSolicitud> aeronaves)
        {
            var sb = new StringBuilder();
            sb.AppendLine("1. Limitada a las estaciones, rutas y aeronaves expresamente autorizadas por la Autoridad Aeronáutica del Ecuador.");
            sb.AppendLine("2. Queda prohibida la prestación de servicios de cabotaje interno o en estaciones no especificadas.");
            return sb.ToString().Trim();
        }

        private static void ConfigurarPermisosUI(CondicionesLimitacionesViewModel vm, string rol, int usuarioId)
        {
            var esInspector = AocrRolesInstitucionales.EsInspector(rol);
            var esCoordinador = AocrRolesInstitucionales.EsCoordinador(rol);
            var esDircav = AocrRolesInstitucionales.EsDircav(rol);
            var esRt = AocrRolesInstitucionales.EsRt(rol);

            vm.PuedeEditarInspector = esInspector && (vm.Estado == AocrEstadoCl.ClNoGenerada || vm.Estado == AocrEstadoCl.ClBorrador || vm.Estado == AocrEstadoCl.ClDevueltaInspector);
            vm.PuedeRemitirCoordinador = esInspector && (vm.Estado == AocrEstadoCl.ClBorrador || vm.Estado == AocrEstadoCl.ClDevueltaInspector);
            vm.PuedeRevisarCoordinador = esCoordinador && vm.Estado == AocrEstadoCl.ClPendienteCoordinador;
            vm.PuedeDevolverInspector = esCoordinador && vm.Estado == AocrEstadoCl.ClPendienteCoordinador;
            vm.PuedeRemitirDircav = esCoordinador && (vm.Estado == AocrEstadoCl.ClPendienteCoordinador || vm.Estado == AocrEstadoCl.ClDevueltaCoordinador);
            vm.PuedeRevisarDircav = esDircav && (vm.Estado == AocrEstadoCl.ClPendienteDircav || vm.Estado == AocrEstadoCl.ClPendienteFirmaDircav);
            vm.PuedeDevolverCoordinador = esDircav && (vm.Estado == AocrEstadoCl.ClPendienteDircav || vm.Estado == AocrEstadoCl.ClPendienteFirmaDircav);
            vm.PuedeFirmarDircav = esDircav && (vm.Estado == AocrEstadoCl.ClPendienteDircav || vm.Estado == AocrEstadoCl.ClPendienteFirmaDircav);
            vm.PuedeVerVistaPrevia = esInspector || esCoordinador || esDircav;
            vm.PuedeDescargar = vm.TienePdfFirmado && (esDircav || esCoordinador || esInspector || (esRt && vm.ExpedienteListoParaCierre));
        }

        #endregion

        #region 2. Ciclo de Vida: Borrador, Remisiones y Devoluciones

        /// <summary>
        /// Guarda o actualiza el borrador de Condiciones y Limitaciones (Exclusivo Inspector).
        /// </summary>
        public CondicionesLimitacionesResultado GuardarBorrador(CondicionesLimitacionesSaveRequest request, int usuarioId, string usuarioNombre, string rol)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Segregación: solo Inspector puede guardar borrador
            if (!AocrRolesInstitucionales.EsInspector(rol))
            {
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 403,
                    Mensaje = "Acceso denegado: Solo el Inspector asignado puede redactar el borrador de Condiciones y Limitaciones."
                };
            }

            // Validar precondiciones de LV e Informe Técnico
            try
            {
                ValidarPrecondicionesGeneracion(request.SolicitudId);
            }
            catch (Exception ex)
            {
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 409,
                    Mensaje = ex.Message
                };
            }

            var vm = ObtenerOConstruirViewModel(request.SolicitudId, usuarioId, rol);

            var cl = new CondicionesLimitaciones
            {
                Id = vm.Id,
                CodigoSolicitud = request.SolicitudId,
                CodigoInspeccion = request.InspeccionId ?? vm.InspeccionId,
                CodigoInforme = vm.InformeId,
                NumeroAocr = vm.NumeroAocr,
                Compania = vm.Compania,
                OperadorExtranjero = vm.OperadorExtranjero,
                RepresentanteTecnico = vm.RepresentanteTecnico,
                TipoOperacion = vm.TipoOperacion,
                RutasAutorizadas = request.RutasAutorizadas ?? vm.RutasAutorizadas,
                AlcanceAutorizado = request.AlcanceAutorizado ?? vm.AlcanceAutorizado,
                CondicionesAprobadas = request.CondicionesAprobadas,
                Limitaciones = request.Limitaciones,
                Observaciones = request.Observaciones,
                InspectorUsuarioId = usuarioId,
                InspectorNombre = usuarioNombre,
                Estado = AocrEstadoCl.ClBorrador
            };

            var idPersistido = _clDao.GuardarBorrador(cl);

            return new CondicionesLimitacionesResultado
            {
                Exitoso = true,
                HttpStatusCode = 200,
                DocumentoId = idPersistido,
                Version = cl.Version,
                Estado = AocrEstadoCl.ClBorrador,
                Mensaje = "Borrador de Condiciones y Limitaciones guardado correctamente."
            };
        }

        /// <summary>
        /// Inspector remite el borrador al Coordinador.
        /// </summary>
        public CondicionesLimitacionesResultado RemitirACoordinador(int solicitudId, int usuarioId, string usuarioNombre, string rol, string observacion)
        {
            if (!AocrRolesInstitucionales.EsInspector(rol))
            {
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 403, Mensaje = "Acceso denegado: Solo el Inspector puede remitir el borrador a Coordinación." };
            }

            var cl = _clDao.ObtenerPorSolicitudVigente(solicitudId);
            if (cl == null)
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 404, Mensaje = "No existe un borrador de Condiciones y Limitaciones generado." };

            if (cl.Estado != AocrEstadoCl.ClBorrador && cl.Estado != AocrEstadoCl.ClDevueltaInspector)
            {
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 409, Mensaje = $"El documento no se encuentra en estado borrador o devuelto (Estado actual: {cl.Estado})." };
            }

            _clDao.ActualizarEstado(cl.Id, AocrEstadoCl.ClPendienteCoordinador, usuarioId, usuarioNombre, rol, observacion);

            return new CondicionesLimitacionesResultado
            {
                Exitoso = true,
                HttpStatusCode = 200,
                DocumentoId = cl.Id,
                Version = cl.Version,
                Estado = AocrEstadoCl.ClPendienteCoordinador,
                Mensaje = "Condiciones y Limitaciones remitidas exitosamente a Coordinación para su revisión formal."
            };
        }

        /// <summary>
        /// Coordinador devuelve el borrador al Inspector con observación obligatoria.
        /// </summary>
        public CondicionesLimitacionesResultado DevolverAInspector(int solicitudId, int usuarioId, string usuarioNombre, string rol, string observacion)
        {
            if (!AocrRolesInstitucionales.EsCoordinador(rol))
            {
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 403, Mensaje = "Acceso denegado: Solo la Coordinación puede devolver el borrador al Inspector." };
            }

            if (string.IsNullOrWhiteSpace(observacion))
            {
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 400, Mensaje = "La observación motivada de devolución al Inspector es obligatoria." };
            }

            var cl = _clDao.ObtenerPorSolicitudVigente(solicitudId);
            if (cl == null)
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 404, Mensaje = "No se encontró el documento de Condiciones y Limitaciones." };

            if (cl.Estado != AocrEstadoCl.ClPendienteCoordinador)
            {
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 409, Mensaje = $"El documento no se encuentra pendiente de revisión de Coordinación (Estado actual: {cl.Estado})." };
            }

            _clDao.ActualizarEstado(cl.Id, AocrEstadoCl.ClDevueltaInspector, usuarioId, usuarioNombre, rol, observacion);

            return new CondicionesLimitacionesResultado
            {
                Exitoso = true,
                HttpStatusCode = 200,
                DocumentoId = cl.Id,
                Version = cl.Version,
                Estado = AocrEstadoCl.ClDevueltaInspector,
                Mensaje = "Condiciones y Limitaciones observadas y devueltas formalmente al Inspector responsable."
            };
        }

        /// <summary>
        /// Coordinador remite el borrador a DIRCAV para su revisión y firma.
        /// </summary>
        public CondicionesLimitacionesResultado RemitirADircav(int solicitudId, int usuarioId, string usuarioNombre, string rol, string observacion)
        {
            if (!AocrRolesInstitucionales.EsCoordinador(rol))
            {
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 403, Mensaje = "Acceso denegado: Solo la Coordinación puede remitir el documento a DIRCAV." };
            }

            var cl = _clDao.ObtenerPorSolicitudVigente(solicitudId);
            if (cl == null)
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 404, Mensaje = "No se encontró el documento de Condiciones y Limitaciones." };

            if (cl.Estado != AocrEstadoCl.ClPendienteCoordinador && cl.Estado != AocrEstadoCl.ClDevueltaCoordinador)
            {
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 409, Mensaje = $"El documento no está habilitado para remisión a DIRCAV (Estado actual: {cl.Estado})." };
            }

            _clDao.ActualizarEstado(cl.Id, AocrEstadoCl.ClPendienteFirmaDircav, usuarioId, usuarioNombre, rol, observacion);

            return new CondicionesLimitacionesResultado
            {
                Exitoso = true,
                HttpStatusCode = 200,
                DocumentoId = cl.Id,
                Version = cl.Version,
                Estado = AocrEstadoCl.ClPendienteFirmaDircav,
                Mensaje = "Condiciones y Limitaciones remitidas exitosamente a la Autoridad DIRCAV para su firma institucional."
            };
        }

        /// <summary>
        /// DIRCAV devuelve el documento a Coordinación con observación obligatoria.
        /// </summary>
        public CondicionesLimitacionesResultado DevolverACoordinador(int solicitudId, int usuarioId, string usuarioNombre, string rol, string observacion)
        {
            if (!AocrRolesInstitucionales.EsDircav(rol))
            {
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 403, Mensaje = "Acceso denegado: Solo la Autoridad DIRCAV puede devolver el documento a Coordinación." };
            }

            if (string.IsNullOrWhiteSpace(observacion))
            {
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 400, Mensaje = "La observación motivada de devolución a Coordinación es obligatoria." };
            }

            var cl = _clDao.ObtenerPorSolicitudVigente(solicitudId);
            if (cl == null)
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 404, Mensaje = "No se encontró el documento de Condiciones y Limitaciones." };

            if (cl.Estado != AocrEstadoCl.ClPendienteDircav && cl.Estado != AocrEstadoCl.ClPendienteFirmaDircav)
            {
                return new CondicionesLimitacionesResultado { Exitoso = false, HttpStatusCode = 409, Mensaje = $"El documento no se encuentra pendiente de revisión de DIRCAV (Estado actual: {cl.Estado})." };
            }

            _clDao.ActualizarEstado(cl.Id, AocrEstadoCl.ClDevueltaCoordinador, usuarioId, usuarioNombre, rol, observacion);

            return new CondicionesLimitacionesResultado
            {
                Exitoso = true,
                HttpStatusCode = 200,
                DocumentoId = cl.Id,
                Version = cl.Version,
                Estado = AocrEstadoCl.ClDevueltaCoordinador,
                Mensaje = "Condiciones y Limitaciones devueltas formalmente a Coordinación con observaciones."
            };
        }

        #endregion

        #region 3. Firma Institucional Exclusiva DIRCAV e Inmutabilidad

        /// <summary>
        /// DIRCAV aplica la firma institucional exclusiva sobre Condiciones y Limitaciones.
        /// Valida precondiciones, genera el PDF oficial definitivo, calcula hash SHA-256,
        /// asegura inmutabilidad y previene doble clic/reintentos concurrentes.
        /// DIRDAC, Coordinador, Inspector y Administrador quedan terminantemente bloqueados (403).
        /// </summary>
        public CondicionesLimitacionesResultado FirmarCondicionesLimitaciones(CondicionesLimitacionesFirmaRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // 1. Segregación estricta: Solo DIRCAV
            if (!AocrRolesInstitucionales.EsDircav(request.RolSolicitante))
            {
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 403,
                    Mensaje = "Acceso denegado: La firma institucional de Condiciones y Limitaciones es competencia exclusiva de la Autoridad DIRCAV. DIRDAC y otros roles no están autorizados."
                };
            }

            var cl = _clDao.ObtenerPorSolicitudVigente(request.SolicitudId);
            if (cl == null)
            {
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 404,
                    Mensaje = "No se encontró el documento de Condiciones y Limitaciones para firmar."
                };
            }

            // 2. Control de Idempotencia / Prevención de doble clic
            if (string.Equals(cl.Estado, AocrEstadoCl.ClFirmadaDircav, StringComparison.OrdinalIgnoreCase))
            {
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = true,
                    HttpStatusCode = 200,
                    Idempotente = true,
                    DocumentoId = cl.Id,
                    Version = cl.Version,
                    Estado = AocrEstadoCl.ClFirmadaDircav,
                    HashPdf = cl.HashPdfFirmado,
                    RutaPdf = cl.RutaPdfFirmado,
                    Mensaje = "El documento de Condiciones y Limitaciones ya se encontraba debidamente firmado por DIRCAV."
                };
            }

            // 3. Validar estado pendiente de firma
            if (cl.Estado != AocrEstadoCl.ClPendienteDircav && cl.Estado != AocrEstadoCl.ClPendienteFirmaDircav)
            {
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 409,
                    Mensaje = $"El documento no se encuentra en estado pendiente de firma por DIRCAV (Estado actual: {cl.Estado})."
                };
            }

            // 4. Construir modelo tipado para el PDF oficial
            CondicionesLimitacionesPdfViewModel pdfModel;
            try
            {
                pdfModel = ConstruirPdfModel(request.SolicitudId, cl, esVistaPrevia: false, dircavNombre: request.DircavUsuarioNombre);
            }
            catch (Exception ex)
            {
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = $"No se puede firmar el documento: {ex.Message}"
                };
            }

            // 5. Generar PDF definitivo con membrete institucional
            byte[] pdfBytes;
            try
            {
                pdfBytes = GenerarPdfOficial(pdfModel);
            }
            catch (Exception ex)
            {
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 500,
                    Mensaje = $"Error al generar el documento PDF oficial: {ex.Message}"
                };
            }

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 500,
                    Mensaje = "El archivo PDF generado está vacío o es inválido."
                };
            }

            // 6. Calcular Hash SHA-256 e inmutabilidad
            string hashFirmado;
            using (var sha = SHA256.Create())
            {
                hashFirmado = BitConverter.ToString(sha.ComputeHash(pdfBytes)).Replace("-", "").ToUpperInvariant();
            }

            var codigoVerificacion = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant();

            // 7. Almacenamiento seguro bajo ~/App_Data/Uploads/AOCR/Condiciones/{solicitudId}/
            var nombreArchivo = $"Condiciones_Limitaciones_{request.SolicitudId}_v{cl.Version}_Firmado.pdf";
            var rutaVirtual = $"~/App_Data/Uploads/AOCR/Condiciones/{request.SolicitudId}/{nombreArchivo}";
            var rutaFisica = FileStorageHelper.MapVirtualPath(rutaVirtual);
            var carpetaFisica = Path.GetDirectoryName(rutaFisica);

            try
            {
                if (!Directory.Exists(carpetaFisica))
                {
                    Directory.CreateDirectory(carpetaFisica);
                }
                File.WriteAllBytes(rutaFisica, pdfBytes);
            }
            catch (Exception ex)
            {
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 500,
                    Mensaje = $"Error al persistir el archivo firmado en almacenamiento seguro: {ex.Message}"
                };
            }

            // 8. Persistir firma y estado en transacción DB
            var okPersistencia = _clDao.RegistrarFirmaDircav(
                cl.Id,
                rutaVirtual,
                hashFirmado,
                pdfBytes.LongLength,
                request.DircavUsuarioId,
                request.DircavUsuarioNombre,
                codigoVerificacion
            );

            if (!okPersistencia)
            {
                // Rollback de archivo si la BD falló
                try { if (File.Exists(rutaFisica)) File.Delete(rutaFisica); } catch { }
                return new CondicionesLimitacionesResultado
                {
                    Exitoso = false,
                    HttpStatusCode = 500,
                    Mensaje = "Error transaccional al registrar la firma de Condiciones y Limitaciones en base de datos."
                };
            }

            // 9. Verificar regla de cierre institucional dual:
            // La entrega final se habilita ÚNICAMENTE cuando CL está firmada por DIRCAV Y AOCR está firmado por DIRDAC.
            var aocrDoc = _documentoGeneradoDao.ObtenerUltimoPorSolicitudTipo(request.SolicitudId, "RECONOCIMIENTO");
            var aocrFirmado = aocrDoc != null && (string.Equals(aocrDoc.Estado, AocrEstadosProceso.AocrFirmadoDirdac, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(aocrDoc.HashPdfFirmado));

            return new CondicionesLimitacionesResultado
            {
                Exitoso = true,
                HttpStatusCode = 200,
                DocumentoId = cl.Id,
                Version = cl.Version,
                Estado = AocrEstadoCl.ClFirmadaDircav,
                HashPdf = hashFirmado,
                RutaPdf = rutaVirtual,
                ExpedienteFinalizado = aocrFirmado,
                Mensaje = aocrFirmado
                    ? "Condiciones y Limitaciones firmadas formalmente por DIRCAV. Con la firma previa de DIRDAC en el AOCR, el expediente ha completado los requisitos para su cierre."
                    : "Condiciones y Limitaciones firmadas formalmente por DIRCAV. El expediente continúa a la espera de la firma y legalización del AOCR por parte de DIRDAC."
            };
        }

        #endregion

        #region 4. Generación de PDF Oficial (iTextSharp con Membrete DGAC)

        public byte[] GenerarVistaPrevia(int solicitudId, int usuarioId, string rol)
        {
            ValidarRolLectura(rol);

            var cl = _clDao.ObtenerPorSolicitudVigente(solicitudId);
            var pdfModel = ConstruirPdfModel(solicitudId, cl, esVistaPrevia: true);
            return GenerarPdfOficial(pdfModel);
        }

        public byte[] ObtenerDocumentoParaDescarga(int solicitudId, int usuarioId, string rol, out string nombreArchivo)
        {
            if (solicitudId <= 0) throw new ArgumentException("ID de solicitud inválido.");

            // Validar RBAC
            if (AocrRolesInstitucionales.EsAdministrador(rol))
            {
                throw new UnauthorizedAccessException("El rol Administrador no tiene permisos para descargar este documento (Regla 7).");
            }

            var cl = _clDao.ObtenerPorSolicitudVigente(solicitudId);
            if (cl == null || string.IsNullOrWhiteSpace(cl.RutaPdfFirmado))
            {
                throw new FileNotFoundException("No existe un PDF firmado de Condiciones y Limitaciones para esta solicitud.");
            }

            // Validar que RT solo pueda descargar si el expediente finalizó institucionalmente
            if (AocrRolesInstitucionales.EsRt(rol))
            {
                var aocrDoc = _documentoGeneradoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
                var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
                var esModificacion = solicitud != null && solicitud.TipoSolicitud == 3;
                var aocrFirmado = aocrDoc != null && (string.Equals(aocrDoc.Estado, AocrEstadosProceso.AocrFirmadoDirdac, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(aocrDoc.HashPdfFirmado));

                if (!esModificacion && !aocrFirmado)
                {
                    throw new UnauthorizedAccessException("El documento de Condiciones y Limitaciones aún no está disponible para descarga del Operador/RT hasta que culmine la legalización del AOCR por DIRDAC.");
                }
            }

            var rutaFisica = FileStorageHelper.ResolvePhysicalPath(cl.RutaPdfFirmado);
            if (!File.Exists(rutaFisica))
            {
                throw new FileNotFoundException("El archivo físico del documento no fue encontrado en el servidor.");
            }

            nombreArchivo = Path.GetFileName(rutaFisica);
            return File.ReadAllBytes(rutaFisica);
        }

        private CondicionesLimitacionesPdfViewModel ConstruirPdfModel(int solicitudId, CondicionesLimitaciones cl, bool esVistaPrevia, string dircavNombre = null)
        {
            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null) throw new KeyNotFoundException($"Solicitud #{solicitudId} no encontrada.");

            var estaciones = _estacionDao.ListarPorSolicitud(solicitudId) ?? new List<SolicitudEstacionInspeccion>();
            var aeronaves = _aeronaveDao.ObtenerPorSolicitud(solicitudId) ?? new List<AeronaveSolicitud>();
            var inspecciones = _inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>();
            var inspeccion = inspecciones.OrderByDescending(i => i.CodigoInspeccion).FirstOrDefault();
            var informe = inspeccion != null ? _informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion) : null;

            var model = new CondicionesLimitacionesPdfViewModel
            {
                SolicitudId = solicitudId,
                NumeroAocr = cl?.NumeroAocr ?? solicitud.NumeroAOC ?? "AOCR-" + solicitudId,
                Version = cl?.Version ?? 1,
                TipoTramite = ObtenerTipoTramiteTexto(solicitud.TipoSolicitud),
                FechaEmision = cl?.FechaFirmaDircav ?? DateTime.Now,
                FechaVencimiento = solicitud.FechaSolicitud.HasValue ? solicitud.FechaSolicitud.Value.AddYears(1) : (DateTime?)null,
                Compania = cl?.Compania ?? solicitud.RazonSocial ?? solicitud.NombreOperador ?? "Operador Aéreo",
                NombreOperador = cl?.OperadorExtranjero ?? solicitud.NombreOperador ?? solicitud.RazonSocial ?? "Operador Aéreo",
                PaisOperador = solicitud.Pais ?? "Ecuador",
                NumeroAoc = solicitud.NumeroAOC ?? "AOC-" + solicitudId,
                RepresentanteTecnico = cl?.RepresentanteTecnico ?? solicitud.RepresentanteLegal ?? "Representante Técnico",
                CedulaRt = solicitud.CedulaRepresentante ?? "N/A",
                TipoOperacion = cl?.TipoOperacion ?? solicitud.TipoOperacion ?? "TRANSPORTE AÉREO REGULAR / NO REGULAR",
                RutasAutorizadas = cl?.RutasAutorizadas ?? "Rutas internacionales y nacionales aprobadas por la Autoridad Aeronáutica.",
                AlcanceAutorizado = cl?.AlcanceAutorizado ?? "Operaciones de transporte comercial de pasajeros, carga y correo.",
                CondicionesAprobadas = cl?.CondicionesAprobadas ?? ConstruirCondicionesAprobadasPorDefecto(solicitud, estaciones, aeronaves),
                Limitaciones = cl?.Limitaciones ?? ConstruirLimitacionesPorDefecto(solicitud, estaciones, aeronaves),
                Observaciones = cl?.Observaciones ?? (informe != null ? informe.Observaciones : string.Empty),
                InspectorNombre = informe != null && !string.IsNullOrWhiteSpace(informe.UsuarioFirma1) ? informe.UsuarioFirma1 : (inspeccion != null ? (inspeccion.InspectorPrincipalNombre ?? "Inspector Asignado") : "Inspector Asignado"),
                FechaInformeTecnico = informe != null ? (informe.FechaFirma1 ?? informe.CreatedAt) : null,
                NombreDirectorCertificacion = !string.IsNullOrWhiteSpace(dircavNombre) ? dircavNombre : (cl?.DircavNombre ?? "Autoridad DIRCAV"),
                FechaFirmaDircav = cl?.FechaFirmaDircav,
                EsVistaPrevia = esVistaPrevia,
                EstadoDocumento = cl?.Estado ?? AocrEstadoCl.ClBorrador,
                CodigoVerificacion = cl?.CodigoVerificacion ?? Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant()
            };

            // Mapear estaciones independientes (AC-02)
            foreach (var est in estaciones.Where(e => e.Activo))
            {
                model.Estaciones.Add(new CondicionEstacionPdfItem
                {
                    CodigoOaci = est.EstacionCodigo ?? "N/A",
                    NombreAeropuerto = est.EstacionNombre ?? "Estación",
                    Ciudad = est.EstacionNombre ?? "Ciudad",
                    FechasInspeccion = est.RangoFechasTexto,
                    Estado = est.Estado ?? "AUTORIZADA"
                });
            }

            // Mapear aeronaves
            foreach (var aero in aeronaves)
            {
                model.Aeronaves.Add(new CondicionAeronavePdfItem
                {
                    Marca = aero.Marca ?? "Aeronave",
                    Modelo = aero.Modelo ?? "Modelo",
                    Serie = aero.Serie ?? "---",
                    Matricula = aero.Matricula ?? "---",
                    Configuracion = aero.Configuracion ?? "Pax / Carga",
                    EstacionesHabilitadas = "Todas las autorizadas"
                });
            }

            return model;
        }

        private static string ObtenerTipoTramiteTexto(int? tipo)
        {
            switch (tipo)
            {
                case 1: return "EMISIÓN";
                case 2: return "RENOVACIÓN";
                case 3: return "MODIFICACIÓN";
                default: return "EMISIÓN";
            }
        }

        /// <summary>
        /// Genera el contenido binario del PDF oficial de Condiciones y Limitaciones
        /// con iTextSharp y estándares institucionales DGAC.
        /// </summary>
        public byte[] GenerarPdfOficial(CondicionesLimitacionesPdfViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36f, 36f, 90f, 60f);
                var writer = PdfWriter.GetInstance(doc, ms);
                writer.CloseStream = false;

                var server = HttpContext.Current != null ? HttpContext.Current.Server : null;
                writer.PageEvent = PdfBrandingHelper.CreateITextPageEvent(server, "CondicionesLimitaciones");

                doc.AddAuthor("Dirección General de Aviación Civil - DIRCAV");
                doc.AddCreator("Sistema AOCR - Documento Oficial de Condiciones y Limitaciones");
                doc.AddTitle($"Condiciones y Limitaciones - Solicitud #{model.SolicitudId}");
                doc.Open();

                var fuenteTitulo = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.BLACK);
                var fuenteSubtitulo = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(27, 79, 114));
                var fuenteNegrita = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, BaseColor.BLACK);
                var fuenteNormal = FontFactory.GetFont(FontFactory.HELVETICA, 8.5f, BaseColor.BLACK);
                var fuentePequena = FontFactory.GetFont(FontFactory.HELVETICA, 7.5f, BaseColor.DARK_GRAY);
                var fuenteAviso = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, new BaseColor(200, 50, 50));

                // Si es vista previa, marca de agua superior
                if (model.EsVistaPrevia)
                {
                    var pAviso = new Paragraph("*** VISTA PREVIA - BORRADOR NO VÁLIDO PARA OPERACIONES ***", fuenteAviso)
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingAfter = 6f
                    };
                    doc.Add(pAviso);
                }

                // 1. Cabecera Oficial
                var tablaHead = new PdfPTable(2) { WidthPercentage = 100 };
                tablaHead.SetWidths(new float[] { 65f, 35f });

                var celdaTit = new PdfPCell { Border = Rectangle.NO_BORDER, PaddingBottom = 4f };
                celdaTit.AddElement(new Paragraph("DIRECCIÓN GENERAL DE AVIACIÓN CIVIL DEL ECUADOR", fuenteSubtitulo));
                celdaTit.AddElement(new Paragraph("CONDICIONES Y LIMITACIONES DE OPERACIÓN", fuenteTitulo));
                celdaTit.AddElement(new Paragraph("CONDITIONS AND LIMITATIONS (RDAC PARTE 129)", fuentePequena));
                tablaHead.AddCell(celdaTit);

                var celdaMeta = new PdfPCell
                {
                    Border = Rectangle.BOX,
                    BorderColor = new BaseColor(180, 180, 180),
                    BackgroundColor = new BaseColor(248, 249, 250),
                    Padding = 5f
                };
                celdaMeta.AddElement(new Paragraph($"AOCR #: {model.NumeroAocr}", fuenteNegrita));
                celdaMeta.AddElement(new Paragraph($"Trámite: {model.TipoTramite} #{model.SolicitudId}", fuenteNormal));
                celdaMeta.AddElement(new Paragraph($"Emisión: {model.FechaEmision:dd/MM/yyyy}", fuenteNormal));
                celdaMeta.AddElement(new Paragraph($"Vencimiento: {(model.FechaVencimiento.HasValue ? model.FechaVencimiento.Value.ToString("dd/MM/yyyy") : "Según RDAC 129")}", fuenteNormal));
                celdaMeta.AddElement(new Paragraph($"Versión: v{model.Version}", fuentePequena));
                tablaHead.AddCell(celdaMeta);

                doc.Add(tablaHead);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 2. Información del Explotador y RT
                var tablaOp = new PdfPTable(4) { WidthPercentage = 100 };
                tablaOp.SetWidths(new float[] { 22f, 28f, 22f, 28f });
                AgregarFilaCabecera(tablaOp, "1. INFORMACIÓN DEL OPERADOR Y REPRESENTANTE TÉCNICO");
                AgregarPar(tablaOp, "Operador Extranjero:", model.NombreOperador, fuenteNegrita, fuenteNormal);
                AgregarPar(tablaOp, "País del Explotador:", model.PaisOperador, fuenteNegrita, fuenteNormal);
                AgregarPar(tablaOp, "Razón Social:", model.Compania, fuenteNegrita, fuenteNormal);
                AgregarPar(tablaOp, "N° AOC Origen:", model.NumeroAoc, fuenteNegrita, fuenteNormal);
                AgregarPar(tablaOp, "Representante Técnico:", model.RepresentanteTecnico, fuenteNegrita, fuenteNormal);
                AgregarPar(tablaOp, "Cédula / Identificación RT:", model.CedulaRt, fuenteNegrita, fuenteNormal);
                AgregarPar(tablaOp, "Tipo de Operación:", model.TipoOperacion, fuenteNegrita, fuenteNormal);
                AgregarPar(tablaOp, "Alcance Autorizado:", model.AlcanceAutorizado, fuenteNegrita, fuenteNormal);

                doc.Add(tablaOp);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 3. Estaciones y Fechas Independientes (AC-02)
                var tablaEst = new PdfPTable(4) { WidthPercentage = 100 };
                tablaEst.SetWidths(new float[] { 18f, 37f, 30f, 15f });
                AgregarFilaCabecera(tablaEst, "2. ESTACIONES / AEROPUERTOS AUTORIZADOS Y FECHAS DE INSPECCIÓN");
                tablaEst.AddCell(CrearCelda( "OACI/IATA", fuenteNegrita, true));
                tablaEst.AddCell(CrearCelda("Aeropuerto / Ciudad", fuenteNegrita, true));
                tablaEst.AddCell(CrearCelda("Período de Inspección", fuenteNegrita, true));
                tablaEst.AddCell(CrearCelda("Estado", fuenteNegrita, true));

                if (model.Estaciones != null && model.Estaciones.Any())
                {
                    foreach (var est in model.Estaciones)
                    {
                        tablaEst.AddCell(CrearCelda(est.CodigoOaci, fuenteNegrita, false));
                        tablaEst.AddCell(CrearCelda(est.NombreAeropuerto, fuenteNormal, false));
                        tablaEst.AddCell(CrearCelda(est.FechasInspeccion, fuenteNormal, false));
                        tablaEst.AddCell(CrearCelda(est.Estado, fuentePequena, false));
                    }
                }
                else
                {
                    var cVacia = new PdfPCell(new Phrase("Estaciones base según especificaciones de operación autorizadas.", fuenteNormal)) { Colspan = 4, Padding = 5f };
                    tablaEst.AddCell(cVacia);
                }

                doc.Add(tablaEst);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 4. Flota / Equipos Autorizados
                var tablaFlota = new PdfPTable(4) { WidthPercentage = 100 };
                tablaFlota.SetWidths(new float[] { 25f, 25f, 25f, 25f });
                AgregarFilaCabecera(tablaFlota, "3. FLOTA Y EQUIPOS AUTORIZADOS");
                tablaFlota.AddCell(CrearCelda("Marca y Modelo", fuenteNegrita, true));
                tablaFlota.AddCell(CrearCelda("Matrícula", fuenteNegrita, true));
                tablaFlota.AddCell(CrearCelda("Número de Serie", fuenteNegrita, true));
                tablaFlota.AddCell(CrearCelda("Configuración", fuenteNegrita, true));

                if (model.Aeronaves != null && model.Aeronaves.Any())
                {
                    foreach (var aero in model.Aeronaves)
                    {
                        tablaFlota.AddCell(CrearCelda($"{aero.Marca} {aero.Modelo}", fuenteNormal, false));
                        tablaFlota.AddCell(CrearCelda(aero.Matricula, fuenteNegrita, false));
                        tablaFlota.AddCell(CrearCelda(aero.Serie, fuenteNormal, false));
                        tablaFlota.AddCell(CrearCelda(aero.Configuracion, fuenteNormal, false));
                    }
                }
                else
                {
                    var cAero = new PdfPCell(new Phrase("Aeronaves comerciales registradas en el expediente de certificación.", fuenteNormal)) { Colspan = 4, Padding = 5f };
                    tablaFlota.AddCell(cAero);
                }

                doc.Add(tablaFlota);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 5. Condiciones Aprobadas
                var tablaCond = new PdfPTable(1) { WidthPercentage = 100 };
                AgregarFilaCabecera(tablaCond, "4. CONDICIONES APROBADAS");
                var cCond = new PdfPCell(new Phrase(model.CondicionesAprobadas ?? "Sin condiciones particulares registradas.", fuenteNormal))
                {
                    Padding = 6f,
                    Border = Rectangle.BOX,
                    BorderColor = new BaseColor(210, 210, 210)
                };
                tablaCond.AddCell(cCond);
                doc.Add(tablaCond);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 6. Limitaciones
                var tablaLim = new PdfPTable(1) { WidthPercentage = 100 };
                AgregarFilaCabecera(tablaLim, "5. LIMITACIONES DE OPERACIÓN");
                var cLim = new PdfPCell(new Phrase(model.Limitaciones ?? "Sin limitaciones adicionales a las contempladas en la RDAC Parte 129.", fuenteNormal))
                {
                    Padding = 6f,
                    Border = Rectangle.BOX,
                    BorderColor = new BaseColor(210, 210, 210)
                };
                tablaLim.AddCell(cLim);
                doc.Add(tablaLim);
                doc.Add(new Paragraph(" ", fuentePequena));

                // 7. Bloque de Firma Institucional DIRCAV
                var tablaFirma = new PdfPTable(2) { WidthPercentage = 100 };
                tablaFirma.SetWidths(new float[] { 50f, 50f });

                var celdaDircav = new PdfPCell
                {
                    Border = Rectangle.BOX,
                    BorderColor = new BaseColor(180, 180, 180),
                    BackgroundColor = new BaseColor(252, 252, 253),
                    Padding = 7f
                };
                celdaDircav.AddElement(new Paragraph("AUTORIDAD DE FIRMA DIRCAV (EXCLUSIVO)", fuenteNegrita));
                celdaDircav.AddElement(new Paragraph($"Director: {model.NombreDirectorCertificacion}", fuenteNormal));
                celdaDircav.AddElement(new Paragraph(model.CargoDirectorCertificacion, fuentePequena));
                celdaDircav.AddElement(new Paragraph($"Fecha Firma: {(model.FechaFirmaDircav.HasValue ? model.FechaFirmaDircav.Value.ToString("dd/MM/yyyy HH:mm:ss") : (model.EsVistaPrevia ? "PENDIENTE DE FIRMA" : DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")))}", fuentePequena));
                tablaFirma.AddCell(celdaDircav);

                var celdaInteg = new PdfPCell
                {
                    Border = Rectangle.BOX,
                    BorderColor = new BaseColor(180, 180, 180),
                    BackgroundColor = new BaseColor(252, 252, 253),
                    Padding = 7f
                };
                celdaInteg.AddElement(new Paragraph("SUSTENTO TÉCNICO E INTEGRIDAD", fuenteNegrita));
                celdaInteg.AddElement(new Paragraph($"Inspector Responsable: {model.InspectorNombre}", fuenteNormal));
                celdaInteg.AddElement(new Paragraph($"Fecha Informe Técnico: {(model.FechaInformeTecnico.HasValue ? model.FechaInformeTecnico.Value.ToString("dd/MM/yyyy") : "N/A")}", fuentePequena));
                celdaInteg.AddElement(new Paragraph($"Cód. Verificación: {model.CodigoVerificacion}", fuentePequena));
                tablaFirma.AddCell(celdaInteg);

                doc.Add(tablaFirma);

                doc.Close();
                return ms.ToArray();
            }
        }

        private static void AgregarFilaCabecera(PdfPTable table, string titulo)
        {
            var celda = new PdfPCell(new Phrase(titulo, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, BaseColor.WHITE)))
            {
                Colspan = table.NumberOfColumns,
                BackgroundColor = new BaseColor(27, 79, 114),
                Padding = 4f,
                HorizontalAlignment = Element.ALIGN_LEFT
            };
            table.AddCell(celda);
        }

        private static void AgregarPar(PdfPTable table, string etiqueta, string valor, Font fLabel, Font fVal)
        {
            var c1 = new PdfPCell(new Phrase(etiqueta, fLabel))
            {
                BackgroundColor = new BaseColor(245, 247, 250),
                Padding = 3.5f,
                BorderColor = new BaseColor(220, 220, 220)
            };
            var c2 = new PdfPCell(new Phrase(valor ?? "---", fVal))
            {
                Padding = 3.5f,
                BorderColor = new BaseColor(220, 220, 220)
            };
            table.AddCell(c1);
            table.AddCell(c2);
        }

        private static PdfPCell CrearCelda(string texto, Font fuente, bool esHeader)
        {
            return new PdfPCell(new Phrase(texto ?? "---", fuente))
            {
                BackgroundColor = esHeader ? new BaseColor(235, 240, 245) : BaseColor.WHITE,
                Padding = 3.5f,
                BorderColor = new BaseColor(220, 220, 220)
            };
        }

        private static void ValidarRolLectura(string rol)
        {
            if (AocrRolesInstitucionales.EsAdministrador(rol))
            {
                throw new UnauthorizedAccessException("El rol Administrador no tiene permisos para operar en Condiciones y Limitaciones (Regla 7).");
            }
            if (AocrRolesInstitucionales.EsDirdac(rol))
            {
                throw new UnauthorizedAccessException("DIRDAC revisa y firma exclusivamente el AOCR; no tiene acceso a la gestión de Condiciones y Limitaciones.");
            }
        }

        #endregion
    }
}
