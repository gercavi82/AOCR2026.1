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
        private readonly SolicitudEstacionDAO _estacionDAO;

        public SolicitudEstacionService()
            : this(new SolicitudEstacionDAO())
        {
        }

        public SolicitudEstacionService(SolicitudEstacionDAO estacionDAO)
        {
            _estacionDAO = estacionDAO ?? new SolicitudEstacionDAO();
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
        public ValidacionEstacionesResultado ValidarEstaciones(IEnumerable<SolicitudEstacionInspeccion> estaciones)
        {
            var resultado = new ValidacionEstacionesResultado { EsValido = true };
            if (estaciones == null) return resultado;

            var lista = estaciones.Where(e => e != null).ToList();
            var codigosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lista.Count; i++)
            {
                var est = lista[i];
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
                res.Mensaje = "Identificador de solicitud inválido.";
                return res;
            }

            var validacion = ValidarEstaciones(estaciones);
            if (!validacion.EsValido)
            {
                res.Exitoso = false;
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
                res.Mensaje = ok
                    ? "Estaciones y fechas de inspección guardadas correctamente."
                    : "No se pudieron guardar las estaciones de inspección.";
                return res;
            }
            catch (Exception ex)
            {
                res.Exitoso = false;
                res.Mensaje = "Error al guardar estaciones de inspección: " + ex.Message;
                return res;
            }
        }
    }

    public class ValidacionEstacionesResultado
    {
        public bool EsValido { get; set; }
        public List<string> Errores { get; set; } = new List<string>();
    }

    public class ResultadoOperacionEstaciones
    {
        public bool Exitoso { get; set; }
        public int SolicitudId { get; set; }
        public string Mensaje { get; set; }
        public List<string> Errores { get; set; } = new List<string>();
    }
}
