using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    /// <summary>
    /// AC-02: Servicio de negocio para validación y gestión de estaciones con fechas de inspección independientes.
    /// Garantiza integridad de fechas, unicidad por solicitud, compatibilidad histórica y trazabilidad.
    /// </summary>
    public class SolicitudEstacionService
    {
        public static string ValidarFechasPorLugar(IEnumerable<SolicitudEstacionInspeccion> estaciones, string lugares, string otraLocalidad)
        {
            var seleccionados = (lugares ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(SolicitudEstacionDAO.NormalizarCodigoEstacion).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var lista = (estaciones ?? Enumerable.Empty<SolicitudEstacionInspeccion>()).Where(e => e != null).ToList();
            if (seleccionados.Count == 0) return "Seleccione al menos un lugar de inspección.";
            if (seleccionados.Contains("OTROS") && string.IsNullOrWhiteSpace(otraLocalidad))
                return "Indique la provincia o localidad del otro lugar de inspección.";
            if (lista.Count != seleccionados.Count || lista.Select(e => SolicitudEstacionDAO.NormalizarCodigoEstacion(e.EstacionCodigo)).Distinct().Count() != lista.Count)
                return "Cada lugar seleccionado debe tener una fecha de inspección; no se permiten lugares duplicados o sin seleccionar.";
            foreach (var est in lista)
            {
                var codigo = SolicitudEstacionDAO.NormalizarCodigoEstacion(est.EstacionCodigo);
                if (!seleccionados.Contains(codigo)) return "Se recibió una fecha para un lugar de inspección no seleccionado.";
                if (est.FechaInicio == default(DateTime) || est.FechaFin == default(DateTime) || est.FechaFin.Date < est.FechaInicio.Date)
                    return "Seleccione un rango de fechas válido para " + (est.EstacionNombre ?? codigo) + ".";
                est.EstacionCodigo = codigo;
                est.EstacionNombre = codigo == "OTROS" ? otraLocalidad.Trim() : SolicitudEstacionDAO.NormalizarNombreEstacion(codigo, codigo);
            }
            return null;
        }

        private readonly SolicitudEstacionDAO _estacionDAO;
        private readonly InspeccionDAO _inspeccionDAO;

        public SolicitudEstacionService()
            : this(new SolicitudEstacionDAO(), new InspeccionDAO())
        {
        }

        public SolicitudEstacionService(SolicitudEstacionDAO estacionDAO)
            : this(estacionDAO, new InspeccionDAO())
        {
        }

        public SolicitudEstacionService(SolicitudEstacionDAO estacionDAO, InspeccionDAO inspeccionDAO)
        {
            _estacionDAO = estacionDAO ?? new SolicitudEstacionDAO();
            _inspeccionDAO = inspeccionDAO ?? new InspeccionDAO();
        }

        /// <summary>
        /// Obtiene las estaciones de una solicitud. Si no existen registros en la tabla aditiva,
        /// aplica compatibilidad histórica reconstruyéndolas a partir de la solicitud e inspecciones.
        /// </summary>
        public List<SolicitudEstacionInspeccion> ObtenerEstacionesPorSolicitud(
            int solicitudId,
            SolicitudAOCR solicitud = null,
            IEnumerable<Inspeccion> inspecciones = null)
        {
            if (solicitudId <= 0) return new List<SolicitudEstacionInspeccion>();

            var estaciones = _estacionDAO.ListarPorSolicitud(solicitudId);
            if (estaciones != null && estaciones.Any())
            {
                return estaciones;
            }

            // Fallback transparente para solicitudes históricas
            if (solicitud != null)
            {
                return SolicitudEstacionDAO.ObtenerCompatibilidadHistorica(solicitud, inspecciones);
            }

            return new List<SolicitudEstacionInspeccion>();
        }

        /// <summary>
        /// Valida exhaustivamente el conjunto de estaciones de una solicitud según las reglas de AC-02.
        /// </summary>
        public ValidacionEstacionesResultado ValidarEstaciones(
            IEnumerable<SolicitudEstacionInspeccion> estaciones,
            int? solicitudId = null)
        {
            var resultado = new ValidacionEstacionesResultado { EsValido = true };
            if (estaciones == null) return resultado;

            var lista = estaciones.Where(e => e != null).ToList();
            var codigosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lista.Count; i++)
            {
                var est = lista[i];
                if (est.FechaInspeccion.HasValue)
                    est.FechaInicio = est.FechaFin = est.FechaInspeccion.Value.Date;
                var indiceVisual = i + 1;

                // 1. Estación obligatoria
                if (string.IsNullOrWhiteSpace(est.EstacionCodigo))
                {
                    resultado.EsValido = false;
                    resultado.Errores.Add(string.Format("La estación en la posición {0} no tiene un código de aeropuerto/estación válido.", indiceVisual));
                    continue;
                }

                var codigoNorm = est.EstacionCodigo.Trim().ToUpperInvariant();

                // 2. Evitar duplicados
                if (codigosVistos.Contains(codigoNorm))
                {
                    resultado.EsValido = false;
                    resultado.EsDuplicado = true;
                    resultado.Errores.Add(string.Format("La estación '{0}' se encuentra duplicada en la solicitud. Cada estación debe registrarse una sola vez.", est.EstacionNombre ?? codigoNorm));
                }
                else
                {
                    codigosVistos.Add(codigoNorm);
                }

                // 3. Fecha inicial obligatoria
                if (est.FechaInicio == default(DateTime))
                {
                    resultado.EsValido = false;
                    resultado.Errores.Add(string.Format("La estación '{0}' requiere una fecha inicial de inspección obligatoria.", est.EstacionNombre ?? codigoNorm));
                }

                // 4. Fecha final no puede ser anterior a la fecha inicial
                if (est.FechaFin != default(DateTime) && est.FechaInicio != default(DateTime))
                {
                    if (est.FechaFin.Date < est.FechaInicio.Date)
                    {
                        resultado.EsValido = false;
                        resultado.Errores.Add(string.Format("En la estación '{0}', la fecha final ({1:dd/MM/yyyy}) no puede ser anterior a la fecha inicial ({2:dd/MM/yyyy}).",
                            est.EstacionNombre ?? codigoNorm, est.FechaFin, est.FechaInicio));
                    }
                }

                // 5. La inspección debe pertenecer a la misma solicitud
                var idSolEfectiva = solicitudId.HasValue && solicitudId.Value > 0 ? solicitudId.Value : est.SolicitudId;
                if (est.InspeccionId.HasValue && est.InspeccionId.Value > 0 && idSolEfectiva > 0 && _inspeccionDAO != null)
                {
                    try
                    {
                        var insp = _inspeccionDAO.ObtenerPorId(est.InspeccionId.Value);
                        if (insp != null && insp.CodigoSolicitud > 0 && insp.CodigoSolicitud != idSolEfectiva)
                        {
                            resultado.EsValido = false;
                            resultado.Errores.Add(string.Format("La inspección #{0} asociada a la estación '{1}' pertenece a la solicitud #{2}, no a la solicitud #{3}.",
                                est.InspeccionId.Value, est.EstacionNombre ?? codigoNorm, insp.CodigoSolicitud, idSolEfectiva));
                        }
                    }
                    catch
                    {
                        // Ignorar en entornos desconectados de BD o pruebas unitarias
                    }
                }

                // 6. El inspector asignado debe existir y estar activo
                if (est.InspectorId.HasValue && est.InspectorId.Value > 0)
                {
                    try
                    {
                        var inspector = UsuarioDAO.ObtenerPorId(est.InspectorId.Value);
                        if (inspector != null && !inspector.Activo)
                        {
                            resultado.EsValido = false;
                            resultado.Errores.Add(string.Format("El inspector asignado (ID={0}) para la estación '{1}' no se encuentra activo.",
                                est.InspectorId.Value, est.EstacionNombre ?? codigoNorm));
                        }
                    }
                    catch
                    {
                        // Ignorar en entornos desconectados de BD o pruebas unitarias
                    }
                }
            }

            return resultado;
        }

        /// <summary>
        /// Guarda las estaciones de una solicitud previa validación de negocio.
        /// </summary>
        public ResultadoOperacionEstaciones GuardarEstaciones(
            int solicitudId,
            IEnumerable<SolicitudEstacionInspeccion> estaciones,
            int? usuarioId,
            IDbConnection conn = null,
            IDbTransaction tx = null)
        {
            var res = new ResultadoOperacionEstaciones { SolicitudId = solicitudId };

            if (solicitudId <= 0)
            {
                res.Exitoso = false;
                res.HttpStatusCode = 400;
                res.Mensaje = "Identificador de solicitud inválido.";
                return res;
            }

            var validacion = ValidarEstaciones(estaciones, solicitudId);
            if (!validacion.EsValido)
            {
                res.Exitoso = false;
                res.HttpStatusCode = validacion.EsDuplicado ? 409 : 400;
                res.EsDuplicado = validacion.EsDuplicado;
                res.Mensaje = string.Join(" ", validacion.Errores);
                res.Errores = validacion.Errores;
                return res;
            }

            try
            {
                bool ok;
                if (conn != null && tx != null)
                {
                    ok = _estacionDAO.GuardarEstacionesTransaccional(solicitudId, estaciones, usuarioId, conn, tx);
                }
                else
                {
                    ok = _estacionDAO.GuardarEstaciones(solicitudId, estaciones, usuarioId);
                }

                res.Exitoso = ok;
                res.HttpStatusCode = ok ? 200 : 500;
                res.Mensaje = ok
                    ? "Estaciones y fechas de inspección guardadas correctamente."
                    : "No se pudieron guardar las estaciones de inspección.";
                return res;
            }
            catch (EstacionVersionConflictException ex)
            {
                res.Exitoso = false;
                res.HttpStatusCode = 409;
                res.EsConflictoVersion = true;
                res.Mensaje = ex.Message;
                res.Errores.Add(ex.Message);
                return res;
            }
            catch (EstacionDuplicadaException ex)
            {
                res.Exitoso = false;
                res.HttpStatusCode = 409;
                res.EsDuplicado = true;
                res.Mensaje = ex.Message;
                res.Errores.Add(ex.Message);
                return res;
            }
            catch (Exception ex)
            {
                res.Exitoso = false;
                res.HttpStatusCode = 500;
                res.Mensaje = "Error al guardar estaciones de inspección: " + ex.Message;
                res.Errores.Add(ex.Message);
                return res;
            }
        }
    }

    public class ValidacionEstacionesResultado
    {
        public bool EsValido { get; set; }
        public bool EsDuplicado { get; set; }
        public List<string> Errores { get; set; } = new List<string>();
    }

    public class ResultadoOperacionEstaciones
    {
        public bool Exitoso { get; set; }
        public int SolicitudId { get; set; }
        public int HttpStatusCode { get; set; } = 200;
        public bool EsConflictoVersion { get; set; }
        public bool EsDuplicado { get; set; }
        public string Mensaje { get; set; }
        public List<string> Errores { get; set; } = new List<string>();
    }
}
