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
            return todas.Where(s =>
                string.Equals(s.Estado, AocrEstadosProceso.PendienteCoordinador, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, EstadoSolicitud.AceptacionDocumental, System.StringComparison.OrdinalIgnoreCase)
            )
            .OrderByDescending(s => s.UpdatedAt ?? s.FechaSolicitud ?? System.DateTime.MinValue)
            .ToList();
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
