using System.Collections.Generic;
using System.Diagnostics;
using CapaDatos.DAOs;
using CapaDatos.Models;

namespace CapaNegocio.Services
{
    public sealed class FirmaInstitucionalAocrService
    {
        private readonly FirmaInstitucionalAocrDAO _dao;public FirmaInstitucionalAocrService():this(new FirmaInstitucionalAocrDAO()){}public FirmaInstitucionalAocrService(FirmaInstitucionalAocrDAO dao){_dao=dao;}
        public IList<FirmaInstitucionalAocrFilaDto> ObtenerPendientes(){Trace.TraceInformation("[DIRDAC_TRAY][QUERY_IN]");var x=_dao.ObtenerPendientes();Trace.TraceInformation("[DIRDAC_TRAY][QUERY_OUT] Total="+x.Count);return x;}
        public int ContarPendientes(){var n=_dao.ContarPendientes();Trace.TraceInformation("[DIRDAC_TRAY][COUNT] Total="+n);return n;}
        public int ContarPendientesDgac(){var n=_dao.ContarPendientesDgac();Trace.TraceInformation("[DGAC_TRAY][COUNT] Total="+n);return n;}
        public int ContarPendientesDcav(){var n=_dao.ContarPendientesDcav();Trace.TraceInformation("[DCAV_SIGNATURE_TRAY][COUNT] Total="+n);return n;}
        public FirmaInstitucionalAocrDetalleDto ObtenerDetalle(int solicitudId,int inspeccionId){if(solicitudId<=0||inspeccionId<=0)return null;return _dao.ObtenerDetalle(solicitudId,inspeccionId);}
    }
}
