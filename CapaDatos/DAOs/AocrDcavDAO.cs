using System.Collections.Generic;
using CapaDatos.Constants;
using CapaDatos.Models;

namespace CapaDatos.DAOs
{
    public sealed class AocrDcavDAO
    {
        private readonly AocrProcesoEstadoDAO _estadoDao;

        public AocrDcavDAO()
            : this(new AocrProcesoEstadoDAO())
        {
        }

        public AocrDcavDAO(AocrProcesoEstadoDAO estadoDao)
        {
            _estadoDao = estadoDao ?? new AocrProcesoEstadoDAO();
        }

        public List<AocrProcesoEstadoRecord> ObtenerPendientesRevisionInforme()
        {
            return _estadoDao.ListarActivosPorEstado(AocrEstadosProceso.PendienteRevisionInformeDcav)
                ?? new List<AocrProcesoEstadoRecord>();
        }

        public List<AocrProcesoEstadoRecord> ObtenerPendientesRevisionDocumentos()
        {
            return _estadoDao.ListarActivosPorEstado(AocrEstadosProceso.PendienteRevisionDocumentosDcav)
                ?? new List<AocrProcesoEstadoRecord>();
        }

        public List<AocrProcesoEstadoRecord> ObtenerObservados()
        {
            return _estadoDao.ListarActivosPorEstado(
                    AocrEstadosProceso.InformeTecnicoObservadoDcav,
                    AocrEstadosProceso.DocumentosObservadosDcav)
                ?? new List<AocrProcesoEstadoRecord>();
        }

        public List<AocrProcesoEstadoRecord> ObtenerLegacyPendientesRevisionDcav()
        {
            return _estadoDao.ListarActivosPorEstado(AocrEstadosProceso.PendienteRevisionDcav)
                ?? new List<AocrProcesoEstadoRecord>();
        }

        public List<AocrProcesoEstadoRecord> ObtenerBandejaRevision()
        {
            var items = new List<AocrProcesoEstadoRecord>();
            items.AddRange(ObtenerPendientesRevisionInforme());
            items.AddRange(ObtenerPendientesRevisionDocumentos());
            items.AddRange(ObtenerObservados());
            items.AddRange(ObtenerLegacyPendientesRevisionDcav());
            return items;
        }
    }
}
