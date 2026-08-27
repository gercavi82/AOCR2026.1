using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaModelo;
using CapaNegocio.Helpers;

namespace CapaNegocio.Services
{
    public sealed class AocrProcesoActivoInfo
    {
        public bool ExisteProcesoActivo { get; set; }
        public SolicitudAOCR SolicitudActiva { get; set; }
        public OrdenRecaudacion OrdenActiva { get; set; }
        public string NumeroSolicitudActiva { get; set; }
        public string NumeroOrdenActiva { get; set; }
        public string EstadoProcesoActivo { get; set; }
        public string MensajeBloqueo { get; set; }
        public string MensajeInformativo { get; set; }
    }

    /// <summary>
    /// Valida proceso AOCR activo por compañía (solicitud u orden no final).
    /// </summary>
    public sealed class AocrProcesoActivoService
    {
        public const string MensajeBloqueoGenerico = OrdenRecaudacionOperativaHelper.MensajeBloqueoNuevaOrdenProcesoActivo;

        private readonly AocrCompaniaContextService _companiaContext = new AocrCompaniaContextService();
        private readonly AocrEstadoService _estadoService = new AocrEstadoService();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly OrdenRecaudacionDAO _ordenDao = new OrdenRecaudacionDAO();

        public AocrProcesoActivoInfo ObtenerProcesoActivoPorCompania(
            int usuarioId,
            string companiaCodigo,
            string companiaNombre = null)
        {
            var info = new AocrProcesoActivoInfo();
            if (usuarioId <= 0 || string.IsNullOrWhiteSpace(companiaCodigo))
            {
                info.MensajeInformativo = "Seleccione una compañía activa para consultar el estado del proceso AOCR.";
                return info;
            }

            var nombre = !string.IsNullOrWhiteSpace(companiaNombre)
                ? companiaNombre.Trim()
                : _companiaContext.ResolverNombreCompaniaAsignada(usuarioId, companiaCodigo);

            var solicitudes = _companiaContext.FiltrarSolicitudesPorCompania(
                _solicitudDao.ObtenerPorUsuario(usuarioId),
                companiaCodigo,
                nombre);

            info.SolicitudActiva = solicitudes
                .Where(s => s != null && s.CodigoSolicitud > 0)
                .Where(s => _estadoService.EsEstadoActivoProceso(s.Estado))
                .OrderByDescending(s => s.CodigoSolicitud)
                .FirstOrDefault();

            var ordenes = _companiaContext.FiltrarOrdenesPorCompania(
                _ordenDao.ListarPorUsuario(usuarioId, null),
                companiaCodigo,
                nombre,
                usuarioId);

            info.OrdenActiva = ordenes
                .Where(o => o != null && o.Id > 0)
                .Where(o => !EstadoOrden.EsEstadoFinal(o.Estado)
                    || (info.SolicitudActiva != null
                        && o.CodigoSolicitud.HasValue
                        && o.CodigoSolicitud.Value == info.SolicitudActiva.CodigoSolicitud
                        && (EstadoOrden.EsPagado(o.Estado)
                            || EstadoOrden.EsOrdenCerradaPostAprobacionFinanciera(o.Estado))))
                .OrderByDescending(o => o.FechaCreacion)
                .ThenByDescending(o => o.Id)
                .FirstOrDefault();

            info.ExisteProcesoActivo = info.SolicitudActiva != null || info.OrdenActiva != null;
            info.NumeroSolicitudActiva = info.SolicitudActiva != null ? info.SolicitudActiva.NumeroSolicitud : null;
            info.NumeroOrdenActiva = info.OrdenActiva != null ? info.OrdenActiva.NumeroOrden : null;
            info.EstadoProcesoActivo = info.SolicitudActiva != null
                ? (info.SolicitudActiva.Estado ?? string.Empty)
                : (info.OrdenActiva != null ? info.OrdenActiva.Estado : null);

            if (info.ExisteProcesoActivo)
            {
                info.MensajeBloqueo = ConstruirMensajeBloqueo(nombre, info);
            }
            else
            {
                info.MensajeInformativo = "No existe un proceso AOCR activo para esta compañía. Puede iniciar una nueva orden de recaudación.";
            }

            return info;
        }

        public bool ExisteProcesoActivoPorCompania(int usuarioId, string companiaCodigo, string companiaNombre = null)
        {
            return ObtenerProcesoActivoPorCompania(usuarioId, companiaCodigo, companiaNombre).ExisteProcesoActivo;
        }

        public bool PuedeCrearNuevaOrden(int usuarioId, string companiaCodigo, string companiaNombre, out string mensaje)
        {
            mensaje = string.Empty;
            var info = ObtenerProcesoActivoPorCompania(usuarioId, companiaCodigo, companiaNombre);
            if (!info.ExisteProcesoActivo)
            {
                return true;
            }

            if (info.OrdenActiva != null
                && string.Equals(EstadoOrden.NormalizarEstado(info.OrdenActiva.Estado), EstadoOrden.Borrador, StringComparison.OrdinalIgnoreCase))
            {
                mensaje = "Existe una orden en borrador para esta compañía. Debe completarla o anularla antes de crear otra.";
                return false;
            }

            mensaje = info.MensajeBloqueo ?? MensajeBloqueoGenerico;
            return false;
        }

        public bool PuedeCrearNuevaSolicitud(int usuarioId, string companiaCodigo, string companiaNombre, out string mensaje)
        {
            mensaje = string.Empty;
            var info = ObtenerProcesoActivoPorCompania(usuarioId, companiaCodigo, companiaNombre);
            if (!info.ExisteProcesoActivo)
            {
                return true;
            }

            if (info.SolicitudActiva != null
                && EstadoSolicitud.PermiteEdicionFormularioEmision(info.SolicitudActiva.Estado))
            {
                mensaje = "Ya existe una solicitud AOCR activa para esta compañía. Debe continuar el trámite actual.";
                return false;
            }

            mensaje = info.MensajeBloqueo ?? MensajeBloqueoGenerico;
            return false;
        }

        public string ObtenerMensajeBloqueoProcesoActivo(int usuarioId, string companiaCodigo, string companiaNombre = null)
        {
            var info = ObtenerProcesoActivoPorCompania(usuarioId, companiaCodigo, companiaNombre);
            return info.ExisteProcesoActivo
                ? (info.MensajeBloqueo ?? MensajeBloqueoGenerico)
                : info.MensajeInformativo;
        }

        public OrdenRecaudacion ObtenerOrdenPendienteAccionPorCompania(
            int usuarioId,
            string companiaCodigo,
            string companiaNombre = null)
        {
            var nombre = !string.IsNullOrWhiteSpace(companiaNombre)
                ? companiaNombre
                : _companiaContext.ResolverNombreCompaniaAsignada(usuarioId, companiaCodigo);

            return _companiaContext.FiltrarOrdenesPorCompania(
                    _ordenDao.ListarPorUsuario(usuarioId, null),
                    companiaCodigo,
                    nombre,
                    usuarioId)
                .Where(o => o != null)
                .Where(o =>
                {
                    var estado = EstadoOrden.NormalizarEstado(o.Estado);
                    return estado == EstadoOrden.Borrador
                        || estado == EstadoOrden.Pendiente
                        || estado == EstadoOrden.Generada
                        || estado == EstadoOrden.Devuelta;
                })
                .OrderBy(o =>
                {
                    var estado = EstadoOrden.NormalizarEstado(o.Estado);
                    if (estado == EstadoOrden.Devuelta) return 0;
                    if (estado == EstadoOrden.Pendiente) return 1;
                    if (estado == EstadoOrden.Generada) return 2;
                    if (estado == EstadoOrden.Borrador) return 3;
                    return 4;
                })
                .ThenByDescending(o => o.FechaCreacion)
                .ThenByDescending(o => o.Id)
                .FirstOrDefault();
        }

        public bool TieneOrdenActivaEnProcesoPorCompania(int usuarioId, string companiaCodigo, string companiaNombre = null)
        {
            var nombre = !string.IsNullOrWhiteSpace(companiaNombre)
                ? companiaNombre
                : _companiaContext.ResolverNombreCompaniaAsignada(usuarioId, companiaCodigo);

            return _companiaContext.FiltrarOrdenesPorCompania(
                    _ordenDao.ListarPorUsuario(usuarioId, null),
                    companiaCodigo,
                    nombre,
                    usuarioId)
                .Any(o => o != null && !EstadoOrden.EsEstadoFinal(o.Estado)
                    && !string.Equals(EstadoOrden.NormalizarEstado(o.Estado), EstadoOrden.Borrador, StringComparison.OrdinalIgnoreCase));
        }

        public bool TieneOrdenPendienteComprobantePorCompania(int usuarioId, string companiaCodigo, string companiaNombre = null)
        {
            var nombre = !string.IsNullOrWhiteSpace(companiaNombre)
                ? companiaNombre
                : _companiaContext.ResolverNombreCompaniaAsignada(usuarioId, companiaCodigo);

            return _companiaContext.FiltrarOrdenesPorCompania(
                    _ordenDao.ListarPorUsuario(usuarioId, null),
                    companiaCodigo,
                    nombre,
                    usuarioId)
                .Any(o =>
                {
                    if (o == null) return false;
                    var estado = EstadoOrden.NormalizarEstado(o.Estado);
                    return estado == EstadoOrden.Pendiente
                        || estado == EstadoOrden.Generada
                        || estado == EstadoOrden.Devuelta;
                });
        }

        public bool TieneOrdenBorradorPorCompania(int usuarioId, string companiaCodigo, string companiaNombre = null)
        {
            var nombre = !string.IsNullOrWhiteSpace(companiaNombre)
                ? companiaNombre
                : _companiaContext.ResolverNombreCompaniaAsignada(usuarioId, companiaCodigo);

            return _companiaContext.FiltrarOrdenesPorCompania(
                    _ordenDao.ListarPorUsuario(usuarioId, null),
                    companiaCodigo,
                    nombre,
                    usuarioId)
                .Any(o => o != null
                    && string.Equals(EstadoOrden.NormalizarEstado(o.Estado), EstadoOrden.Borrador, StringComparison.OrdinalIgnoreCase));
        }

        private static string ConstruirMensajeBloqueo(string nombreCompania, AocrProcesoActivoInfo info)
        {
            var compania = !string.IsNullOrWhiteSpace(nombreCompania) ? nombreCompania : "la compañía seleccionada";
            var referencia = !string.IsNullOrWhiteSpace(info.NumeroSolicitudActiva)
                ? info.NumeroSolicitudActiva
                : info.NumeroOrdenActiva;

            if (!string.IsNullOrWhiteSpace(referencia))
            {
                return OrdenRecaudacionOperativaHelper.MensajeBloqueoNuevaOrdenProcesoActivo
                    + " Referencia activa: " + referencia + ".";
            }

            return MensajeBloqueoGenerico;
        }
    }
}
