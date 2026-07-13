using System.Diagnostics;
using CapaDatos.DAOs;
using CapaNegocio.DTOs;
using Npgsql;

namespace CapaNegocio.Services
{
    public sealed class CondicionesBorradorService : ICondicionesBorradorService
    {
        private readonly AocrDocumentoGeneradoDAO _dao;
        public CondicionesBorradorService() : this(new AocrDocumentoGeneradoDAO()) { }
        public CondicionesBorradorService(AocrDocumentoGeneradoDAO dao) { _dao=dao; }

        public ResultadoBorradorDocumento ObtenerOCrearBorrador(NpgsqlConnection connection,NpgsqlTransaction transaction,BorradorDocumentoRequest request)
        {
            Trace.TraceInformation("[CONDICIONES][BORRADOR_SEARCH] SolicitudId="+request.SolicitudId+"; InspeccionId="+request.InspeccionId+";");
            bool creado;
            var doc=_dao.ObtenerOCrearBorrador(connection,transaction,request.SolicitudId,request.InspeccionId,request.CodigoCompania,request.InspectorId,"CONDICIONES_LIMITACIONES",request.UsuarioCreadorId,out creado);
            Trace.TraceInformation((creado?"[CONDICIONES][BORRADOR_CREATED]":"[CONDICIONES][BORRADOR_FOUND]")+" DocumentoId="+doc.CodigoDocumento+";");
            return new ResultadoBorradorDocumento { Exitoso=doc!=null&&doc.CodigoDocumento>0, Creado=creado, Documento=doc };
        }
    }
}
