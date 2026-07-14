using System;
using Npgsql;
using NpgsqlTypes;
using CapaDatos.Infrastructure;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    public interface IGate8EventoRepository
    {
        bool RegistrarIntento(Gate8EventoRegistro evento);
        void ActualizarResultado(string eventKey, string resultado, string detalleError);
    }

    public sealed class Gate8EventoRegistro
    {
        public string Evento, EventKey, CorrelationId, Modulo, Accion, Entidad, EstadoAnterior, EstadoNuevo;
        public string Usuario, Rol, Ip, Observacion, Hash, Resultado, DetalleError;
        public int? EntidadId, SolicitudId, InspeccionId, InformeId, NcId, DocumentoId, UsuarioId, Version;
    }

    public sealed class Gate8EventoDAO : BaseDAO, IGate8EventoRepository
    {
        public Gate8EventoDAO() : base(new SecureConfigurationService().GetConnectionString("PostgreSQL") ?? "") { }

        public bool RegistrarIntento(Gate8EventoRegistro e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.EventKey)) throw new ArgumentException("event_key es obligatorio.");
            return ExecuteInTransaction((cn, tx) =>
            {
                const string sql = @"INSERT INTO public.aocr_evento_workflow
(evento,event_key,correlation_id,modulo,accion,entidad,entidad_id,solicitud_id,inspeccion_id,informe_id,nc_id,documento_id,
 estado_anterior,estado_nuevo,usuario_id,usuario,rol,ip,observacion,version,hash,resultado,detalle_error,intentos,fecha,updated_at)
VALUES(@evento,@key,@corr,@modulo,@accion,@entidad,@entidad_id,@solicitud,@inspeccion,@informe,@nc,@documento,
 @anterior,@nuevo,@usuario_id,@usuario,@rol,@ip,@observacion,@version,@hash,@resultado,@error,1,NOW(),NOW())
ON CONFLICT(event_key) DO UPDATE SET intentos=aocr_evento_workflow.intentos+1,updated_at=NOW()
RETURNING (xmax = 0);";
                using (var cmd = new NpgsqlCommand(sql, cn, tx))
                {
                    P(cmd,"@evento",e.Evento); P(cmd,"@key",e.EventKey); P(cmd,"@corr",e.CorrelationId); P(cmd,"@modulo",e.Modulo);
                    P(cmd,"@accion",e.Accion); P(cmd,"@entidad",e.Entidad); P(cmd,"@entidad_id",e.EntidadId); P(cmd,"@solicitud",e.SolicitudId);
                    P(cmd,"@inspeccion",e.InspeccionId); P(cmd,"@informe",e.InformeId); P(cmd,"@nc",e.NcId); P(cmd,"@documento",e.DocumentoId);
                    P(cmd,"@anterior",e.EstadoAnterior); P(cmd,"@nuevo",e.EstadoNuevo); P(cmd,"@usuario_id",e.UsuarioId); P(cmd,"@usuario",e.Usuario);
                    P(cmd,"@rol",e.Rol); P(cmd,"@ip",e.Ip); P(cmd,"@observacion",e.Observacion); P(cmd,"@version",e.Version);
                    P(cmd,"@hash",e.Hash); P(cmd,"@resultado",e.Resultado ?? "REGISTRADO"); P(cmd,"@error",e.DetalleError);
                    return Convert.ToBoolean(cmd.ExecuteScalar());
                }
            });
        }

        public void ActualizarResultado(string eventKey, string resultado, string detalleError)
        {
            ExecuteWithConnection(cn => { using(var cmd=new NpgsqlCommand("UPDATE public.aocr_evento_workflow SET resultado=@r,detalle_error=@e,updated_at=NOW() WHERE event_key=@k",cn)) { P(cmd,"@r",resultado);P(cmd,"@e",detalleError);P(cmd,"@k",eventKey);cmd.ExecuteNonQuery(); } });
        }
        private static void P(NpgsqlCommand c,string n,object v){c.Parameters.AddWithValue(n,v??DBNull.Value);}
    }
}
