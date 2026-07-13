using System.Diagnostics;
using CapaDatos.DAOs;
using CapaNegocio.DTOs;
using Npgsql;

namespace CapaNegocio.Services
{
    public sealed class AocrBorradorService : IAocrBorradorService
    {
        private readonly AocrDocumentoGeneradoDAO _dao;
        public AocrBorradorService() : this(new AocrDocumentoGeneradoDAO()) { }
        public AocrBorradorService(AocrDocumentoGeneradoDAO dao) { _dao=dao; }

        public ResultadoBorradorDocumento ObtenerOCrearBorrador(NpgsqlConnection connection,NpgsqlTransaction transaction,BorradorDocumentoRequest request)
        {
            Trace.TraceInformation("[AOCR][BORRADOR_SEARCH] SolicitudId="+request.SolicitudId+"; InspeccionId="+request.InspeccionId+";");
            bool creado;
            var doc=_dao.ObtenerOCrearBorrador(connection,transaction,request.SolicitudId,request.InspeccionId,request.CodigoCompania,request.InspectorId,"RECONOCIMIENTO",request.UsuarioCreadorId,out creado);
            Trace.TraceInformation((creado?"[AOCR][BORRADOR_CREATED]":"[AOCR][BORRADOR_FOUND]")+" DocumentoId="+doc.CodigoDocumento+";");
            return new ResultadoBorradorDocumento { Exitoso=doc!=null&&doc.CodigoDocumento>0, Creado=creado, Documento=doc };
        }
    }
}
