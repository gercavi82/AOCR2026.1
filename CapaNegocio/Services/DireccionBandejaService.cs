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
            return new FirmaInstitucionalAocrService().ContarPendientesDgac();
        }
    }
}
