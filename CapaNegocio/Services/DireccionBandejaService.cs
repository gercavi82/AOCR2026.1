using System.Collections.Generic;
using CapaDatos.DAOs;

namespace CapaNegocio.Services
{
    public sealed class DireccionBandejaService
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();

        public List<CapaModelo.InspeccionInformeTecnico> ObtenerPendientesRevisionDcav()
        {
            return _informeDao.ListarPendientesRevisionInformeDcav() ?? new List<CapaModelo.InspeccionInformeTecnico>();
        }

        public int ContarPendientesRevisionDcav()
        {
            return ObtenerPendientesRevisionDcav().Count;
        }

        public int ContarFirmasPendientesDirdac()
        {
            return (_informeDao.ListarPendientesFirmaDirdac() ?? new List<CapaModelo.InspeccionInformeTecnico>()).Count;
        }
    }
}
