using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;
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
                var estadosIntento = ConstruirEstadosIntento(cn, doc.Estado);
                var tieneCreatedAt = ExisteColumna(cn, "aocr_tbdocumento", "created_at");
                var columnaUsuario = ExisteColumna(cn, "aocr_tbdocumento", "created_by")
                    ? "created_by"
                    : (ExisteColumna(cn, "aocr_tbdocumento", "usuario_registro") ? "usuario_registro" : string.Empty);

                var columnas = new List<string>
                {
                    "codigo_solicitud",
                    "tipo_documento",
                    "nombre_archivo",
                    "ruta_guardada",
                    "tipo",
                    "extension",
                    "tamano_bytes",
                    "estado",
                    "validado",
                    "fecha_carga",
                    "observaciones",
                    "version"
                };

                var valores = new List<string>
                {
                    "@codigo_solicitud",
                    "@tipo_documento",
                    "@nombre_archivo",
                    "@ruta_guardada",
                    "@tipo",
                    "@extension",
                    "@tamano_bytes",
                    "@estado",
                    "@validado",
                    "@fecha_carga",
                    "@observaciones",
                    "@version"
                };

                if (tieneCreatedAt)
                {
                    columnas.Add("created_at");
                    valores.Add("NOW()");
                }

                if (!string.IsNullOrWhiteSpace(columnaUsuario))
                {
                    columnas.Add(columnaUsuario);
                    valores.Add("@created_by");
                }

                string sql = $@"
                    INSERT INTO aocr_tbdocumento
                    ({string.Join(", ", columnas)})
                    VALUES
                    ({string.Join(", ", valores)})
                    RETURNING codigo_documento;";

                for (var i = 0; i < estadosIntento.Count; i++)
                {
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

                        var estado = estadosIntento[i];
                        var pEstado = new NpgsqlParameter("@estado", NpgsqlDbType.Varchar);
                        pEstado.Value = (object)estado ?? DBNull.Value;
                        cmd.Parameters.Add(pEstado);
                        cmd.Parameters.AddWithValue("@validado", (object)doc.Validado ?? false);

                        cmd.Parameters.AddWithValue("@fecha_carga", (object)doc.FechaCarga ?? DateTime.Now);
                        cmd.Parameters.AddWithValue("@observaciones", (object)doc.Observaciones ?? DBNull.Value);

                        cmd.Parameters.AddWithValue("@version", (object)doc.Version ?? DBNull.Value);

                        // Tu propiedad UsuarioRegistro se guarda en created_by
                        if (!string.IsNullOrWhiteSpace(columnaUsuario))
                        {
                            cmd.Parameters.AddWithValue("@created_by", (object)doc.UsuarioRegistro ?? "sistema");
                        }

                        try
                        {
                            return Convert.ToInt32(cmd.ExecuteScalar());
                        }
                        catch (PostgresException ex) when (ex.SqlState == "23514" &&
                                                           i < estadosIntento.Count - 1)
                        {
                            // Reintentamos con el siguiente estado candidato ante cualquier CHECK de insercion.
                            continue;
                        }
                    }
                }

                throw new InvalidOperationException(
                    "No se pudo insertar documento con ningun estado compatible para chk_estado_documento. " +
                    "Estados intentados: " + string.Join(", ", estadosIntento.Select(x => x ?? "<NULL>")));
            }
        }

        private static List<string> ConstruirEstadosIntento(NpgsqlConnection cn, string estadoSolicitado)
        {
            var candidatos = new List<string>();

            var estadoResuelto = ResolverEstadoParaInsercion(cn, estadoSolicitado);
            if (!string.IsNullOrWhiteSpace(estadoResuelto))
            {
                candidatos.Add(estadoResuelto);
            }

            var permitidos = ObtenerEstadosPermitidos(cn);
            foreach (var estado in permitidos)
            {
                if (!candidatos.Any(x => string.Equals(x, estado, StringComparison.OrdinalIgnoreCase)))
                {
                    candidatos.Add(estado);
                }
            }

            foreach (var fallback in new[] { "Cargado", "CARGADO", "PENDIENTE", "REGISTRADO", "BORRADOR", "ACTIVO", "A" })
            {
                if (permitidos.Count == 0 &&
                    !candidatos.Any(x => string.Equals(x, fallback, StringComparison.OrdinalIgnoreCase)))
                {
                    candidatos.Add(fallback);
                }
            }

            if (ColumnaEstadoPermiteNull(cn))
            {
                candidatos.Add(null);
            }

            return candidatos;
        }

        private static string ResolverEstadoParaInsercion(NpgsqlConnection cn, string estadoSolicitado)
        {
            var estado = (estadoSolicitado ?? string.Empty).Trim();
            if (string.Equals(estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase))
            {
                estado = "Cargado";
            }
            else if (string.Equals(estado, "ELIMINADO", StringComparison.OrdinalIgnoreCase))
            {
                estado = "Rechazado";
            }

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

            foreach (var preferido in new[] { "Cargado", "En Revisión", "Aprobado", "Rechazado", "Subsanado", "PENDIENTE", "REGISTRADO", "BORRADOR" })
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

        private static bool ColumnaEstadoPermiteNull(NpgsqlConnection cn)
        {
            const string sql = @"
                SELECT is_nullable
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'aocr_tbdocumento'
                  AND column_name = 'estado'
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                var isNullable = cmd.ExecuteScalar() as string;
                return string.Equals(isNullable, "YES", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool ExisteColumna(NpgsqlConnection cn, string tabla, string columna)
        {
            const string sql = @"
                SELECT 1
                FROM pg_attribute a
                WHERE a.attrelid = to_regclass(@tabla)
                  AND a.attname = @columna
                  AND a.attnum > 0
                  AND NOT a.attisdropped
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                cmd.Parameters.AddWithValue("@columna", columna);
                return cmd.ExecuteScalar() != null;
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
                var estadoPersistencia = ResolverEstadoParaInsercion(cn, doc.Estado);
                var tieneUpdatedAt = ExisteColumna(cn, "aocr_tbdocumento", "updated_at");
                var columnaUpdatedBy = ExisteColumna(cn, "aocr_tbdocumento", "updated_by")
                    ? "updated_by"
                    : (ExisteColumna(cn, "aocr_tbdocumento", "usuario_actualizacion") ? "usuario_actualizacion" : string.Empty);

                var setParts = new List<string>
                {
                    "tipo_documento = @tipo_documento",
                    "nombre_archivo = @nombre_archivo",
                    "ruta_guardada = @ruta_guardada",
                    "extension = @extension",
                    "tamano_bytes = @tamano_bytes",
                    "estado = @estado",
                    "validado = @validado",
                    "fecha_carga = @fecha_carga",
                    "observaciones = @observaciones",
                    "version = @version"
                };

                if (tieneUpdatedAt)
                {
                    setParts.Add("updated_at = NOW()");
                }

                if (!string.IsNullOrWhiteSpace(columnaUpdatedBy))
                {
                    setParts.Add(columnaUpdatedBy + " = @updated_by");
                }

                string sql = @"
                    UPDATE aocr_tbdocumento
                    SET " + string.Join(", ", setParts) + @"
                    WHERE codigo_documento = @codigo_documento;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@tipo_documento", (object)doc.TipoDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nombre_archivo", (object)doc.NombreArchivo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_guardada", (object)doc.RutaGuardada ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@extension", (object)doc.Extension ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tamano_bytes", (object)doc.TamanoBytes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado", (object)estadoPersistencia ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@validado", (object)doc.Validado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_carga", (object)doc.FechaCarga ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observaciones", (object)doc.Observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@version", (object)doc.Version ?? DBNull.Value);
                    if (!string.IsNullOrWhiteSpace(columnaUpdatedBy))
                    {
                        cmd.Parameters.AddWithValue("@updated_by", (object)doc.UsuarioRegistro ?? "sistema");
                    }
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
                var estadoPersistencia = ResolverEstadoParaInsercion(cn, "ELIMINADO");
                var tieneUpdatedAt = ExisteColumna(cn, "aocr_tbdocumento", "updated_at");
                var columnaUpdatedBy = ExisteColumna(cn, "aocr_tbdocumento", "updated_by")
                    ? "updated_by"
                    : (ExisteColumna(cn, "aocr_tbdocumento", "usuario_actualizacion") ? "usuario_actualizacion" : string.Empty);

                var setParts = new List<string>
                {
                    "estado = @estado"
                };

                if (tieneUpdatedAt)
                {
                    setParts.Add("updated_at = NOW()");
                }

                if (!string.IsNullOrWhiteSpace(columnaUpdatedBy))
                {
                    setParts.Add(columnaUpdatedBy + " = @u");
                }

                string sql = @"
                    UPDATE aocr_tbdocumento
                    SET " + string.Join(", ", setParts) + @"
                    WHERE codigo_documento = @id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoDocumento);
                    cmd.Parameters.AddWithValue("@estado", (object)estadoPersistencia ?? DBNull.Value);
                    if (!string.IsNullOrWhiteSpace(columnaUpdatedBy))
                    {
                        cmd.Parameters.AddWithValue("@u", usuario ?? "sistema");
                    }
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
