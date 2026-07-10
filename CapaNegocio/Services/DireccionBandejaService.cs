using System.Collections.Generic;
using CapaDatos.Constants;
using CapaDatos.DAOs;

namespace CapaNegocio.Services
{
    public sealed class DireccionBandejaService
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();
        private readonly AocrProcesoEstadoDAO _procesoEstadoDao = new AocrProcesoEstadoDAO();

        public List<CapaModelo.SolicitudAOCR> ObtenerBandejaEjecutivaAprobacion()
        {
            return _solicitudDao.ObtenerParaBandejaEjecutivaAprobacion()
                ?? new List<CapaModelo.SolicitudAOCR>();
        }

        public int ContarBandejaEjecutivaAprobacion()
        {
            return ObtenerBandejaEjecutivaAprobacion().Count;
        }

        public int ContarFirmasPendientesDirdac()
        {
            var estadosFirmaAocr = _procesoEstadoDao.ListarActivosPorEstado(
                AocrEstadosProceso.PendienteFirmaDirectorGeneral,
                AocrEstadosProceso.AocrFirmadoDirdac,
                AocrEstadosProceso.CondicionesFirmadasDirdac,
                AocrEstadosProceso.PendienteFirmaDirectorGeneralLegacy,
                "PENDIENTE_FIRMA_DIRECCION");

            var pendientesInformeLegacy = _informeDao.ListarPendientesFirmaDirdac()
                ?? new List<CapaModelo.InspeccionInformeTecnico>();

            return (estadosFirmaAocr != null ? estadosFirmaAocr.Count : 0) + pendientesInformeLegacy.Count;
        }
    }
}
