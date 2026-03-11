using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class DocumentoDAO
    {
        private string ConnectionString =>
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        // =========================================================
        // INSERTAR DOCUMENTO
        // =========================================================
        public int Crear(Documento doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var estadoInsert = ResolverEstadoParaInsercion(cn, doc.Estado);

                string sql = @"
                    INSERT INTO aocr_tbdocumento
                    (codigo_solicitud, tipo_documento, nombre_archivo, ruta_guardada, tipo,
                     extension, tamano_bytes, estado, validado, fecha_carga, observaciones,
                     version, created_at, created_by)
                    VALUES
                    (@codigo_solicitud, @tipo_documento, @nombre_archivo, @ruta_guardada, @tipo,
                     @extension, @tamano_bytes, @estado, @validado, @fecha_carga, @observaciones,
                     @version, NOW(), @created_by)
                    RETURNING codigo_documento;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", doc.CodigoSolicitud);

                    cmd.Parameters.AddWithValue("@tipo_documento", (object)doc.TipoDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nombre_archivo", (object)doc.NombreArchivo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_guardada", (object)doc.RutaGuardada ?? DBNull.Value);

                    // Columna "tipo" existe en tu tabla; si no la usas, mandamos "ARCHIVO"
                    cmd.Parameters.AddWithValue("@tipo", (object)"ARCHIVO");

                    cmd.Parameters.AddWithValue("@extension", (object)doc.Extension ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tamano_bytes", (object)doc.TamanoBytes ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@estado", (object)estadoInsert ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@validado", (object)doc.Validado ?? false);

                    cmd.Parameters.AddWithValue("@fecha_carga", (object)doc.FechaCarga ?? DateTime.Now);
                    cmd.Parameters.AddWithValue("@observaciones", (object)doc.Observaciones ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@version", (object)doc.Version ?? DBNull.Value);

                    // Tu propiedad UsuarioRegistro se guarda en created_by
                    cmd.Parameters.AddWithValue("@created_by", (object)doc.UsuarioRegistro ?? "sistema");

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        private static string ResolverEstadoParaInsercion(NpgsqlConnection cn, string estadoSolicitado)
        {
            var estado = (estadoSolicitado ?? string.Empty).Trim();
            var permitidos = ObtenerEstadosPermitidos(cn);

            if (permitidos.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(estado) || string.Equals(estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase))
                {
                    return "Cargado";
                }

                return estado;
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                var exacto = permitidos.FirstOrDefault(x => string.Equals(x, estado, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exacto))
                {
                    return exacto;
                }
            }

            foreach (var preferido in new[] { "Cargado", "PENDIENTE", "REGISTRADO", "BORRADOR" })
            {
                var encontrado = permitidos.FirstOrDefault(x => string.Equals(x, preferido, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(encontrado))
                {
                    return encontrado;
                }
            }

            return permitidos[0];
        }

        private static List<string> ObtenerEstadosPermitidos(NpgsqlConnection cn)
        {
            const string sql = @"
                SELECT pg_get_constraintdef(c.oid)
                FROM pg_constraint c
                JOIN pg_class t ON t.oid = c.conrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = 'public'
                  AND t.relname = 'aocr_tbdocumento'
                  AND c.contype = 'c'
                  AND (
                        c.conname = 'chk_estado_documento'
                        OR pg_get_constraintdef(c.oid) ILIKE '%estado%'
                      )
                ORDER BY CASE WHEN c.conname = 'chk_estado_documento' THEN 0 ELSE 1 END
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                var def = cmd.ExecuteScalar() as string;
                if (string.IsNullOrWhiteSpace(def))
                {
                    return new List<string>();
                }

                var valores = Regex.Matches(def, "'([^']+)'")
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return valores;
            }
        }

        // =========================================================
        // OBTENER POR SOLICITUD
        // =========================================================
        public List<Documento> ObtenerPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<Documento>();

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
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
                    WHERE codigo_solicitud = @id
                    ORDER BY fecha_carga DESC NULLS LAST, codigo_documento DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoSolicitud);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            lista.Add(Mapear(rd));
                    }
                }
            }

            return lista;
        }

        // =========================================================
        // OBTENER POR ID
        // =========================================================
        public Documento ObtenerPorId(int codigoDocumento)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
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

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoDocumento);

                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Mapear(rd) : null;
                    }
                }
            }
        }

        // =========================================================
        // OBTENER TODOS
        // =========================================================
        public List<Documento> ObtenerTodos()
        {
            var lista = new List<Documento>();

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
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
                    ORDER BY fecha_carga DESC NULLS LAST, codigo_documento DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(Mapear(rd));
                }
            }

            return lista;
        }

        // =========================================================
        // ACTUALIZAR
        // =========================================================
        public bool Actualizar(Documento doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (doc.CodigoDocumento <= 0) throw new Exception("Código de documento inválido.");

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    UPDATE aocr_tbdocumento
                    SET tipo_documento   = @tipo_documento,
                        nombre_archivo   = @nombre_archivo,
                        ruta_guardada    = @ruta_guardada,
                        extension        = @extension,
                        tamano_bytes     = @tamano_bytes,
                        estado           = @estado,
                        validado         = @validado,
                        fecha_carga      = @fecha_carga,
                        observaciones    = @observaciones,
                        version          = @version,
                        updated_at       = NOW(),
                        updated_by       = @updated_by
                    WHERE codigo_documento = @codigo_documento;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@tipo_documento", (object)doc.TipoDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nombre_archivo", (object)doc.NombreArchivo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_guardada", (object)doc.RutaGuardada ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@extension", (object)doc.Extension ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tamano_bytes", (object)doc.TamanoBytes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado", (object)doc.Estado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@validado", (object)doc.Validado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_carga", (object)doc.FechaCarga ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observaciones", (object)doc.Observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@version", (object)doc.Version ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@updated_by", (object)doc.UsuarioRegistro ?? "sistema");
                    cmd.Parameters.AddWithValue("@codigo_documento", doc.CodigoDocumento);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // =========================================================
        // ELIMINAR (LÓGICO)
        // =========================================================
        public bool MarcarComoEliminado(int codigoDocumento, string usuario)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    UPDATE aocr_tbdocumento
                    SET estado = 'ELIMINADO',
                        updated_at = NOW(),
                        updated_by = @u
                    WHERE codigo_documento = @id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoDocumento);
                    cmd.Parameters.AddWithValue("@u", usuario ?? "sistema");
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // =========================================================
        // MAPEO ÚNICO
        // =========================================================
        private Documento Mapear(IDataRecord rd)
        {
            return new Documento
            {
                CodigoDocumento = rd["codigo_documento"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_documento"]),
                CodigoSolicitud = rd["codigo_solicitud"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_solicitud"]),

                TipoDocumento = rd["tipo_documento"] == DBNull.Value ? null : rd["tipo_documento"].ToString(),
                NombreArchivo = rd["nombre_archivo"] == DBNull.Value ? null : rd["nombre_archivo"].ToString(),
                RutaGuardada = rd["ruta_guardada"] == DBNull.Value ? null : rd["ruta_guardada"].ToString(),

                Extension = rd["extension"] == DBNull.Value ? null : rd["extension"].ToString(),
                TamanoBytes = rd["tamano_bytes"] == DBNull.Value ? (long?)null : Convert.ToInt64(rd["tamano_bytes"]),

                Estado = rd["estado"] == DBNull.Value ? null : rd["estado"].ToString(),
                Validado = rd["validado"] == DBNull.Value ? (bool?)null : Convert.ToBoolean(rd["validado"]),

                FechaCarga = rd["fecha_carga"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["fecha_carga"]),
                Observaciones = rd["observaciones"] == DBNull.Value ? null : rd["observaciones"].ToString(),

                Version = rd["version"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["version"]),
                UsuarioRegistro = rd["created_by"] == DBNull.Value ? null : rd["created_by"].ToString()
            };
        }
    }
}
