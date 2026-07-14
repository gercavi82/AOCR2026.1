using System;
using System.Collections.Generic;
using System.Configuration;
using Npgsql;

namespace CapaDatos.DAOs
{
    public sealed class AocrProcesoEstadoDAO
    {
        private readonly string _cs;
        public AocrProcesoEstadoDAO(){var x=ConfigurationManager.ConnectionStrings["AOCRConnection"];_cs=x!=null&&!string.IsNullOrWhiteSpace(x.ConnectionString)?x.ConnectionString:ConexionDAO.CadenaConexion;}

        public IList<int> ListarInspeccionesActivas(string estado)
        {
            if(string.IsNullOrWhiteSpace(estado))throw new ArgumentException("El estado central es obligatorio.","estado");
            var result=new List<int>();
            const string sql=@"SELECT DISTINCT pe.inspeccion_id FROM public.aocr_proceso_estado pe
JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud=pe.solicitud_id AND s.deleted_at IS NULL
WHERE pe.activo=TRUE AND pe.estado_actual=@estado AND pe.inspeccion_id IS NOT NULL
ORDER BY pe.inspeccion_id;";
            using(var cn=new NpgsqlConnection(_cs))using(var cmd=new NpgsqlCommand(sql,cn)){cmd.Parameters.AddWithValue("@estado",estado);cn.Open();using(var rd=cmd.ExecuteReader())while(rd.Read())result.Add(Convert.ToInt32(rd[0]));}
            return result;
        }

        public void CambiarEstado(int solicitudId,int inspeccionId,string estado,string etapa,string rolResponsable,int usuarioId,string observacion)
        {
            if(solicitudId<=0||inspeccionId<=0||usuarioId<=0)throw new ArgumentOutOfRangeException("El contexto de transición DCAV es inválido.");
            if(string.IsNullOrWhiteSpace(estado)||string.IsNullOrWhiteSpace(rolResponsable))throw new ArgumentException("Estado y rol responsable son obligatorios.");
            using(var cn=new NpgsqlConnection(_cs)){cn.Open();using(var tx=cn.BeginTransaction()){
                using(var close=new NpgsqlCommand("UPDATE public.aocr_proceso_estado SET activo=FALSE,updated_at=NOW(),updated_by=@usuario WHERE solicitud_id=@solicitud AND activo=TRUE;",cn,tx)){close.Parameters.AddWithValue("@usuario",usuarioId);close.Parameters.AddWithValue("@solicitud",solicitudId);close.ExecuteNonQuery();}
                using(var insert=new NpgsqlCommand(@"INSERT INTO public.aocr_proceso_estado(solicitud_id,inspeccion_id,estado_actual,etapa_actual,rol_responsable,observacion,activo,version,created_at,created_by,updated_at,updated_by)
VALUES(@solicitud,@inspeccion,@estado,@etapa,@rol,@observacion,TRUE,1,NOW(),@usuario,NOW(),@usuario);",cn,tx)){insert.Parameters.AddWithValue("@solicitud",solicitudId);insert.Parameters.AddWithValue("@inspeccion",inspeccionId);insert.Parameters.AddWithValue("@estado",estado);insert.Parameters.AddWithValue("@etapa",(object)etapa??DBNull.Value);insert.Parameters.AddWithValue("@rol",rolResponsable);insert.Parameters.AddWithValue("@observacion",(object)observacion??DBNull.Value);insert.Parameters.AddWithValue("@usuario",usuarioId);insert.ExecuteNonQuery();}
                tx.Commit();
            }}
        }
    }
}
