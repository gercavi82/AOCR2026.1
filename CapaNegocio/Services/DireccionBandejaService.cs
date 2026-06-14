using System.Collections.Generic;
using CapaDatos.DAOs;

namespace CapaNegocio.Services
{
    public sealed class DireccionBandejaService
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();

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
            return (_informeDao.ListarPendientesFirmaDirdac() ?? new List<CapaModelo.InspeccionInformeTecnico>()).Count;
        }
    }
}
