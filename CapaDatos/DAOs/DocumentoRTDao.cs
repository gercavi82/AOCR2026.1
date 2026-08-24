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
                                        id::integer AS SolicitudRtId,
                                        'DESIGNACION_RT' AS Tipo,
                                        formulario_designacion_nombre_original AS NombreArchivo,
                                        formulario_designacion_archivo AS RutaStorage,
                                        NULL::bigint AS TamanoBytes,
                                        formulario_designacion_hash AS HashSha256,
                                        formulario_designacion_fecha_carga AS CreatedAt
                                FROM django_aocr_registro_rt
                                WHERE id = @solicitudId
                                    AND formulario_designacion_archivo IS NOT NULL
                                LIMIT 1;";

            using (var cn = CrearConexion())
            {
                return cn.QueryFirstOrDefault<DocumentoModel>(sql, new { solicitudId });
            }
        }

        public void UpsertDocumentoDesignacion(int solicitudId, DocumentoModel doc)
        {
            const string sqlUpdate = @"
                UPDATE django_aocr_registro_rt
                SET formulario_designacion_archivo = @RutaStorage,
                    formulario_designacion_nombre_original = @NombreArchivo,
                    formulario_designacion_hash = @HashSha256,
                    formulario_designacion_fecha_carga = NOW(),
                    actualizado_en = NOW()
                WHERE id = @SolicitudRtId;";

            using (var cn = CrearConexion())
            {
                doc.SolicitudRtId = solicitudId;
                cn.Execute(sqlUpdate, doc);
            }
        }
    }
}
