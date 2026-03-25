using System;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class IntegracionInspeccionOrService
    {
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly HallazgoDAO _hallazgoDao;
        private readonly OrdenRecaudacionDAO _ordenDao;
        private readonly ValidacionDocumentalService _validacionDocumentalService;
        private readonly AuditoriaService _auditoriaService;

        public IntegracionInspeccionOrService()
        {
            _solicitudDao = new SolicitudAOCRDAO();
            _inspeccionDao = new InspeccionDAO();
            _hallazgoDao = new HallazgoDAO();
            _ordenDao = new OrdenRecaudacionDAO();
            _validacionDocumentalService = new ValidacionDocumentalService();
            _auditoriaService = new AuditoriaService();
        }

        public bool PuedeGenerarOR(int idSolicitud)
        {
            if (idSolicitud <= 0)
            {
                return false;
            }

            var solicitud = _solicitudDao.ObtenerPorId(idSolicitud);
            if (solicitud == null)
            {
                return false;
            }

            var inspecciones = _inspeccionDao.ListarPorSolicitud(idSolicitud);
            if (inspecciones == null || inspecciones.Count == 0)
            {
                return false;
            }

            var inspeccionCerrada = inspecciones
                .OrderByDescending(i => i.CodigoInspeccion)
                .FirstOrDefault(i => string.Equals(EstadosInspeccion.NormalizarEstado(i.Estado), EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase));

            if (inspeccionCerrada == null)
            {
                return false;
            }

            var hallazgos = _hallazgoDao.ObtenerPorInspeccion(inspeccionCerrada.CodigoInspeccion);
            var ncAbiertas = hallazgos.Any(h => h != null && !string.Equals((h.Estado ?? string.Empty).Trim(), "CERRADO", StringComparison.OrdinalIgnoreCase));
            if (ncAbiertas)
            {
                return false;
            }

            var validacionDocs = _validacionDocumentalService.PuedeAvanzarEtapa(idSolicitud, "HABILITAR_OR");
            if (!validacionDocs.EsValido)
            {
                return false;
            }

            return true;
        }

        public ResultadoOperacion GenerarORSiCorresponde(int idSolicitud, string usuario)
        {
            try
            {
                if (!PuedeGenerarOR(idSolicitud))
                {
                    return ResultadoOperacion.Error("La solicitud no cumple condiciones para generar OR.");
                }

                // Reusar flujo actual de órdenes: aquí solo se marca habilitación si ya existe,
                // evitando acoplar generación completa en módulo de inspección.
                var estadoFinanciero = ObtenerEstadoFinancieroRelacionado(idSolicitud);
                if (estadoFinanciero != null &&
                    (string.Equals(estadoFinanciero.Estado, "GENERADA", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(estadoFinanciero.Estado, "PAGADA", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(estadoFinanciero.Estado, "COMPLETADA", StringComparison.OrdinalIgnoreCase)))
                {
                    return ResultadoOperacion.Ok(estadoFinanciero, "La OR ya existe para la solicitud.");
                }

                _auditoriaService.RegistrarEvento(
                    modulo: "IntegracionInspeccionOR",
                    accion: "OR_HABILITADA",
                    entidad: "aocr_tbsolicitud",
                    entidadId: idSolicitud,
                    estadoAnterior: null,
                    estadoNuevo: "OR_HABILITADA",
                    usuarioId: null,
                    usuarioNombre: usuario,
                    observacion: "Solicitud habilitada para generación de OR.",
                    ip: null,
                    datosResumen: "Habilitación automática desde cierre de inspección.");

                return ResultadoOperacion.Ok(null, "Solicitud habilitada para generación de OR.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al evaluar generación de OR: " + ex.Message);
            }
        }

        public OrdenRecaudacionModel ObtenerEstadoFinancieroRelacionado(int idSolicitud)
        {
            if (idSolicitud <= 0)
            {
                return null;
            }

            try
            {
                var ordenes = _ordenDao.ObtenerTodasLasOrdenes(null)
                    .Where(o => o != null && o.CodigoSolicitud.HasValue && o.CodigoSolicitud.Value == idSolicitud)
                    .ToList();
                if (ordenes == null || ordenes.Count == 0)
                {
                    return null;
                }

                var ultima = ordenes
                    .OrderByDescending(o => o.FechaCreacion)
                    .FirstOrDefault();

                if (ultima == null)
                {
                    return null;
                }

                return new OrdenRecaudacionModel
                {
                    Id = ultima.Id,
                    CodigoSolicitud = ultima.CodigoSolicitud.HasValue ? ultima.CodigoSolicitud.Value.ToString() : null,
                    NumeroOrden = ultima.NumeroOrden,
                    Estado = ultima.Estado,
                    Total = ultima.Total ?? 0m,
                    FechaCreacion = ultima.FechaCreacion
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
