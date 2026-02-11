using System;
using Dapper;
using Npgsql;
using CapaModelo.RT;

namespace CapaDatos.DAOs
{
    public class DocumentoRTDao
    {
        private NpgsqlConnection CrearConexion() => ConexionDAO.CrearConexion();

        public DocumentoModel GetDocumentoDesignacion(int solicitudId)
        {
            const string sql = @"
                SELECT
                    id AS Id,
                    solicitud_rt_id AS SolicitudRtId,
                    tipo AS Tipo,
                    nombre_archivo AS NombreArchivo,
                    ruta_storage AS RutaStorage,
                    tamano_bytes AS TamanoBytes,
                    hash_sha256 AS HashSha256,
                    created_at AS CreatedAt
                FROM aocr_documento
                WHERE solicitud_rt_id = @solicitudId
                  AND tipo = 'DESIGNACION_RT'
                ORDER BY id DESC
                LIMIT 1;";

            using (var cn = CrearConexion())
            {
                return cn.QueryFirstOrDefault<DocumentoModel>(sql, new { solicitudId });
            }
        }

        public void UpsertDocumentoDesignacion(int solicitudId, DocumentoModel doc)
        {
            const string sqlUpdate = @"
                UPDATE aocr_documento
                SET nombre_archivo = @NombreArchivo,
                    ruta_storage = @RutaStorage,
                    tamano_bytes = @TamanoBytes,
                    hash_sha256 = @HashSha256,
                    created_at = NOW()
                WHERE solicitud_rt_id = @SolicitudRtId
                  AND tipo = 'DESIGNACION_RT';";

            const string sqlInsert = @"
                INSERT INTO aocr_documento
                    (solicitud_rt_id, tipo, nombre_archivo, ruta_storage, tamano_bytes, hash_sha256, created_at)
                VALUES
                    (@SolicitudRtId, 'DESIGNACION_RT', @NombreArchivo, @RutaStorage, @TamanoBytes, @HashSha256, NOW());";

            using (var cn = CrearConexion())
            {
                doc.SolicitudRtId = solicitudId;
                var rows = cn.Execute(sqlUpdate, doc);
                if (rows == 0)
                {
                    cn.Execute(sqlInsert, doc);
                }
            }
        }
    }
}
