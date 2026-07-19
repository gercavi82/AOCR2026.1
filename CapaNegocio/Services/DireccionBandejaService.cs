using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;

namespace CapaNegocio.Services
{
    public sealed class DireccionBandejaService
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();
        private readonly AocrBandejaDAO _aocrBandejaDao = new AocrBandejaDAO();

        public List<CapaModelo.InspeccionInformeTecnico> ObtenerPendientesRevisionDirdac()
        {
            return _informeDao.ListarPendientesRevisionInformeDirdac() ?? new List<CapaModelo.InspeccionInformeTecnico>();
        }

        public int ContarPendientesRevisionDirdac()
        {
            return ObtenerPendientesRevisionDirdac().Count;
        }

        public int ContarPendientesRevisionDcav() { return ContarPendientesRevisionDirdac(); }

        public int ContarFirmasPendientesDirdac()
        {
            // El badge "Informes tecnicos por aprobar" abre la bandeja de
            // revision DCAV. Debe contar exactamente los mismos registros
            // visibles y respetar sus estados centrales y precondiciones.
            return ContarPendientesRevisionDirdac();
        }

        public int ContarAocrPendientesFirmaDirdac()
        {
            return (_aocrBandejaDao.ListarGeneradasFirmadas() ?? new List<CapaModelo.Common.AocrBandejaDocumentoRow>())
                .Count(AocrFirmaPendientePolicy.EsAocrPendienteFirma);
        }

        public int ContarCondicionesPendientesFirmaDcav()
        {
            return (_aocrBandejaDao.ListarGeneradasFirmadas() ?? new List<CapaModelo.Common.AocrBandejaDocumentoRow>())
                .Count(AocrFirmaPendientePolicy.EsCondicionesPendienteFirma);
        }
    }
}
