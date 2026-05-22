using CapaDatos.DAOs;

namespace CapaNegocio.Services
{
    public class PagoService
    {
        private readonly OrdenRecaudacionDAO _ordenDao;

        public PagoService()
            : this(new OrdenRecaudacionDAO())
        {
        }

        public PagoService(OrdenRecaudacionDAO ordenDao)
        {
            _ordenDao = ordenDao ?? new OrdenRecaudacionDAO();
        }

        public bool PagoAprobadoParaSolicitud(int codigoSolicitud)
        {
            return codigoSolicitud > 0 && _ordenDao.TieneAprobacionFinancieraSolicitud(codigoSolicitud);
        }
    }
}