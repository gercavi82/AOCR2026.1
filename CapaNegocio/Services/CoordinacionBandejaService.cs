using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class CoordinacionBandejaService
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly DashboardInspeccionDAO _dashboardDao = new DashboardInspeccionDAO();

        public List<SolicitudAOCR> ObtenerPendientesAsignacion()
        {
            return _solicitudDao.ObtenerPendientesAsignacion() ?? new List<SolicitudAOCR>();
        }

        public int ContarPendientesAsignacion()
        {
            return ObtenerPendientesAsignacion().Count;
        }

        public int ContarColaDocumental(int maxRows = 200)
        {
            return (_dashboardDao.ObtenerControlDocumental(maxRows) ?? new List<DashboardInspeccionDocumentoData>()).Count;
        }

        public List<SolicitudAOCR> ObtenerRevisionesPendientesCoordinador()
        {
            var todas = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return todas.Where(s => s != null && EsRevisionDocumentalPendiente(s.Estado))
            .OrderByDescending(s => s.UpdatedAt ?? s.FechaSolicitud ?? System.DateTime.MinValue)
            .ToList();
        }

        public static bool EsRevisionDocumentalPendiente(string estado)
        {
            var actual = (estado ?? string.Empty).Trim();
            return string.Equals(actual, AocrEstadosProceso.PendienteCoordinador, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, EstadoSolicitud.AceptacionDocumental, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, AocrEstadosProceso.DevueltoCoordinador, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, AocrEstadosProceso.DevueltoCoordinadorPorDircav, System.StringComparison.OrdinalIgnoreCase);
        }

        public int ContarRevisionesPendientesCoordinador()
        {
            return ObtenerRevisionesPendientesCoordinador().Count;
        }

        public int ContarRevisionFormalAocr()
        {
            var estados = new[]
            {
                EstadoSolicitud.GeneradoCondicionesLimitaciones,
                EstadoSolicitud.EnRevisionCoordinadorFinal,
                EstadoSolicitud.FirmadoCoordinador,
                EstadoSolicitud.AOCR_EnRevision
            };

            return (_solicitudDao.ObtenerPorEstados(estados) ?? new List<SolicitudAOCR>())
                .GroupBy(s => s.CodigoSolicitud)
                .Count();
        }
    }
}
