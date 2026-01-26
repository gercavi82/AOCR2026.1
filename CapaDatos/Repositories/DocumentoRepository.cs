using System;
using System.Collections.Generic;
using System.Configuration;
using Npgsql;
using CapaModelo;

namespace CapaDatos.Repositories
{
    public class DocumentoRepository : IDocumentoRepository
    {
        private readonly string _connectionString;

        public DocumentoRepository()
        {
            // PostgreSQL / Npgsql
            _connectionString = ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;
        }

        public int Crear(Documento documento)
        {
            if (documento == null) throw new ArgumentNullException(nameof(documento));

            const string sql = @"
INSERT INTO aocr_tbdocumento
(
  codigo_solicitud,
  tipo_documento,
  nombre_archivo,
  ruta_guardada,
  tipo,
  hash_archivo,
  tamano_bytes,
  extension,
  estado,
  validado,
  fecha_carga,
  observaciones,
  version,
  created_at,
  created_by
)
VALUES
(
  @codigo_solicitud,
  @tipo_documento,
  @nombre_archivo,
  @ruta_guardada,
  @tipo,
  @hash_archivo,
  @tamano_bytes,
  @extension,
  @estado,
  @validado,
  @fecha_carga,
  @observaciones,
  @version,
  now(),
  @created_by
)
RETURNING codigo_documento;";

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_solicitud", documento.CodigoSolicitud);
                cmd.Parameters.AddWithValue("@tipo_documento", (object)(documento.TipoDocumento ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nombre_archivo", (object)(documento.NombreArchivo ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruta_guardada", (object)(documento.RutaGuardada ?? "") ?? DBNull.Value);

                // En tu tabla existe columna "tipo" (varchar 100). Si tu modelo no la tiene, queda vacío.
                // Puedes mapearlo si tienes Documento.Tipo o Documento.MimeType, etc.
                cmd.Parameters.AddWithValue("@tipo", DBNull.Value);

                // En tu tabla existe hash_archivo (varchar 500). Si tu modelo no lo tiene, queda null.
                cmd.Parameters.AddWithValue("@hash_archivo", DBNull.Value);

                cmd.Parameters.AddWithValue("@tamano_bytes", documento.TamanoBytes.HasValue ? (object)documento.TamanoBytes.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@extension", (object)(documento.Extension ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@estado", (object)(documento.Estado ?? "Cargado") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@validado", documento.Validado.HasValue ? (object)documento.Validado.Value : DBNull.Value);

                // Si no viene fecha, usa ahora (Postgres: now())
                cmd.Parameters.AddWithValue("@fecha_carga", documento.FechaCarga.HasValue ? (object)documento.FechaCarga.Value : DateTime.Now);

                cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrWhiteSpace(documento.Observaciones) ? (object)DBNull.Value : documento.Observaciones);
                cmd.Parameters.AddWithValue("@version", documento.Version.HasValue ? (object)documento.Version.Value : 1);

                // created_by en tu tabla (equivalente a UsuarioRegistro)
                cmd.Parameters.AddWithValue("@created_by", string.IsNullOrWhiteSpace(documento.UsuarioRegistro) ? (object)DBNull.Value : documento.UsuarioRegistro);

                cn.Open();
                var id = cmd.ExecuteScalar();
                return Convert.ToInt32(id);
            }
        }

        public bool Actualizar(Documento documento)
        {
            if (documento == null) throw new ArgumentNullException(nameof(documento));

            const string sql = @"
UPDATE aocr_tbdocumento SET
  tipo_documento = @tipo_documento,
  nombre_archivo = @nombre_archivo,
  ruta_guardada = @ruta_guardada,
  tamano_bytes   = @tamano_bytes,
  extension      = @extension,
  estado         = @estado,
  validado       = @validado,
  observaciones  = @observaciones,
  version        = COALESCE(version, 0) + 1
WHERE codigo_documento = @codigo_documento;";

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_documento", documento.CodigoDocumento);
                cmd.Parameters.AddWithValue("@tipo_documento", (object)(documento.TipoDocumento ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nombre_archivo", (object)(documento.NombreArchivo ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruta_guardada", (object)(documento.RutaGuardada ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tamano_bytes", documento.TamanoBytes.HasValue ? (object)documento.TamanoBytes.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@extension", (object)(documento.Extension ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@estado", (object)(documento.Estado ?? "Cargado") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@validado", documento.Validado.HasValue ? (object)documento.Validado.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrWhiteSpace(documento.Observaciones) ? (object)DBNull.Value : documento.Observaciones);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool Eliminar(int id)
        {
            const string sql = "DELETE FROM aocr_tbdocumento WHERE codigo_documento = @id;";

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public Documento ObtenerPorId(int id)
        {
            const string sql = @"
SELECT
  codigo_documento,
  codigo_solicitud,
  tipo_documento,
  nombre_archivo,
  ruta_guardada,
  extension,
  tamano_bytes,
  estado,
  validado,
  fecha_carga,
  observaciones,
  version,
  created_by
FROM aocr_tbdocumento
WHERE codigo_documento = @id;";

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return MapearDocumento(reader);
                }
            }
        }

        public List<Documento> ObtenerPorSolicitud(int solicitudId)
        {
            const string sql = @"
SELECT
  codigo_documento,
  codigo_solicitud,
  tipo_documento,
  nombre_archivo,
  ruta_guardada,
  extension,
  tamano_bytes,
  estado,
  validado,
  fecha_carga,
  observaciones,
  version,
  created_by
FROM aocr_tbdocumento
WHERE codigo_solicitud = @solicitudId
ORDER BY fecha_carga DESC;";

            var lista = new List<Documento>();

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@solicitudId", solicitudId);
                cn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        lista.Add(MapearDocumento(reader));
                }
            }

            return lista;
        }

        public List<Documento> ObtenerPorTipo(int solicitudId, string tipoDocumento)
        {
            const string sql = @"
SELECT
  codigo_documento,
  codigo_solicitud,
  tipo_documento,
  nombre_archivo,
  ruta_guardada,
  extension,
  tamano_bytes,
  estado,
  validado,
  fecha_carga,
  observaciones,
  version,
  created_by
FROM aocr_tbdocumento
WHERE codigo_solicitud = @solicitudId
  AND tipo_documento = @tipoDocumento
ORDER BY fecha_carga DESC;";

            var lista = new List<Documento>();

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@solicitudId", solicitudId);
                cmd.Parameters.AddWithValue("@tipoDocumento", tipoDocumento ?? "");
                cn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        lista.Add(MapearDocumento(reader));
                }
            }

            return lista;
        }

        public List<Documento> ObtenerSubsanaciones(int solicitudId)
        {
            const string sql = @"
SELECT
  codigo_documento,
  codigo_solicitud,
  tipo_documento,
  nombre_archivo,
  ruta_guardada,
  extension,
  tamano_bytes,
  estado,
  validado,
  fecha_carga,
  observaciones,
  version,
  created_by
FROM aocr_tbdocumento
WHERE codigo_solicitud = @solicitudId
  AND (
       COALESCE(observaciones, '') ILIKE '%subsan%'
       OR estado = 'SUBSANACION'
  )
ORDER BY fecha_carga DESC;";

            var lista = new List<Documento>();

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@solicitudId", solicitudId);
                cn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        lista.Add(MapearDocumento(reader));
                }
            }

            return lista;
        }

        public List<Documento> ObtenerPorEstado(int solicitudId, string estado)
        {
            const string sql = @"
SELECT
  codigo_documento,
  codigo_solicitud,
  tipo_documento,
  nombre_archivo,
  ruta_guardada,
  extension,
  tamano_bytes,
  estado,
  validado,
  fecha_carga,
  observaciones,
  version,
  created_by
FROM aocr_tbdocumento
WHERE codigo_solicitud = @solicitudId
  AND estado = @estado
ORDER BY fecha_carga DESC;";

            var lista = new List<Documento>();

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@solicitudId", solicitudId);
                cmd.Parameters.AddWithValue("@estado", estado ?? "");
                cn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        lista.Add(MapearDocumento(reader));
                }
            }

            return lista;
        }

        public bool ValidarDocumento(int documentoId, bool validado, string observaciones, string usuario)
        {
            const string sql = @"
UPDATE aocr_tbdocumento SET
  validado = @validado,
  estado = CASE WHEN @validado THEN 'VALIDADO' ELSE 'RECHAZADO' END,
  observaciones = @observaciones,
  fecha_validacion = now(),
  validado_por = @usuario,
  version = COALESCE(version, 0) + 1
WHERE codigo_documento = @documentoId;";

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@documentoId", documentoId);
                cmd.Parameters.AddWithValue("@validado", validado);
                cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrWhiteSpace(observaciones) ? (object)DBNull.Value : observaciones);
                cmd.Parameters.AddWithValue("@usuario", string.IsNullOrWhiteSpace(usuario) ? (object)DBNull.Value : usuario);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private Documento MapearDocumento(NpgsqlDataReader reader)
        {
            // NOTA: Ajusta si tu clase Documento tiene nombres distintos.
            return new Documento
            {
                CodigoDocumento = reader.GetInt32(reader.GetOrdinal("codigo_documento")),
                CodigoSolicitud = reader.GetInt32(reader.GetOrdinal("codigo_solicitud")),
                TipoDocumento = reader["tipo_documento"] as string,
                NombreArchivo = reader["nombre_archivo"] as string,
                RutaGuardada = reader["ruta_guardada"] as string,
                Extension = reader["extension"] as string,

                TamanoBytes = reader["tamano_bytes"] == DBNull.Value ? (long?)null : Convert.ToInt64(reader["tamano_bytes"]),
                Estado = reader["estado"] as string,

                Validado = reader["validado"] == DBNull.Value ? (bool?)null : Convert.ToBoolean(reader["validado"]),
                FechaCarga = reader["fecha_carga"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["fecha_carga"]),
                Observaciones = reader["observaciones"] == DBNull.Value ? null : (string)reader["observaciones"],
                Version = reader["version"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["version"]),

                // created_by = UsuarioRegistro
                UsuarioRegistro = reader["created_by"] == DBNull.Value ? null : (string)reader["created_by"]
            };
        }
    }
}
