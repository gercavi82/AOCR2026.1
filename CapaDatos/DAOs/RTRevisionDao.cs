using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Npgsql;
using CapaModelo.RT;

namespace CapaDatos.DAOs
{
    public class RTRevisionDao
    {
        private NpgsqlConnection CrearConexion() => ConexionDAO.CrearConexion();

        public RevisionSolicitudRTModel ObtenerPorSolicitud(int solicitudRtId)
        {
            const string sql = @"SELECT id AS Id, solicitud_rt_id AS SolicitudRtId,
                                        inspector_usuario AS InspectorUsuario,
                                        coordinador_usuario_id AS CoordinadorUsuarioId,
                                        estado AS Estado, resultado AS Resultado,
                                        observacion AS Observacion,
                                        fecha_asignacion AS FechaAsignacion,
                                        fecha_revision AS FechaRevision
                                 FROM aocr_solicitud_rt_revision
                                 WHERE solicitud_rt_id = @solicitudRtId
                                   AND estado <> 'CERRADA'
                                 ORDER BY id DESC LIMIT 1;";

            using (var cn = CrearConexion())
            {
                return cn.QueryFirstOrDefault<RevisionSolicitudRTModel>(sql, new { solicitudRtId });
            }
        }

        public void Asignar(int solicitudRtId, string inspectorUsuario, int coordinadorUsuarioId)
        {
            using (var cn = CrearConexion())
            using (var tx = cn.BeginTransaction())
            {
                const string closePrevious = @"UPDATE aocr_solicitud_rt_revision
                                               SET estado = 'CERRADA', updated_at = NOW()
                                               WHERE solicitud_rt_id = @solicitudRtId
                                                 AND estado <> 'CERRADA';";
                cn.Execute(closePrevious, new { solicitudRtId }, tx);

                const string insert = @"INSERT INTO aocr_solicitud_rt_revision
                    (solicitud_rt_id, inspector_usuario, coordinador_usuario_id, estado)
                    VALUES (@solicitudRtId, @inspectorUsuario, @coordinadorUsuarioId, 'ASIGNADA');";
                cn.Execute(insert, new { solicitudRtId, inspectorUsuario, coordinadorUsuarioId }, tx);
                tx.Commit();
            }
        }

        public bool RegistrarResultado(int solicitudRtId, string inspectorUsuario, string resultado, string observacion)
        {
            const string sql = @"UPDATE aocr_solicitud_rt_revision
                                 SET estado = 'DEVUELTA_COORDINADOR', resultado = @resultado,
                                     observacion = @observacion, fecha_revision = NOW(), updated_at = NOW()
                                 WHERE solicitud_rt_id = @solicitudRtId
                                   AND inspector_usuario = @inspectorUsuario
                                   AND estado IN ('ASIGNADA', 'EN_REVISION');";

            using (var cn = CrearConexion())
            {
                return cn.Execute(sql, new { solicitudRtId, inspectorUsuario, resultado, observacion }) > 0;
            }
        }

        public List<int> ObtenerSolicitudesPorInspector(string inspectorUsuario)
        {
            const string sql = @"SELECT solicitud_rt_id
                                 FROM aocr_solicitud_rt_revision
                                 WHERE inspector_usuario = @inspectorUsuario
                                   AND estado IN ('ASIGNADA', 'EN_REVISION');";
            using (var cn = CrearConexion())
            {
                return cn.Query<int>(sql, new { inspectorUsuario }).ToList();
            }
        }
    }
}