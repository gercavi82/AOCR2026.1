using System;
using System.Collections.Generic;
using System.Configuration;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class DocumentoInspeccionDAO
    {
        private readonly string _cs;
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;

        public DocumentoInspeccionDAO()
        {
            var settings = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _cs = (settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString))
                ? settings.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public List<DocumentoInspeccion> ObtenerPorInspeccion(int codigoInspeccion)
        {
            var lista = new List<DocumentoInspeccion>();

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT codigo_documento,
                           codigo_inspeccion,
                           codigo_informe,
                           codigo_documento_base,
                           version,
                           tipo_documento,
                           nombre_archivo_original,
                           nombre_archivo_storage,
                           ruta_archivo,
                           hash_archivo,
                           tamano_bytes,
                           content_type,
                           observacion,
                           subido_por_rol,
                           codigo_usuario,
                           created_at
                    FROM public.aocr_tbdocumento_inspeccion
                    WHERE codigo_inspeccion = @codigo_inspeccion
                    ORDER BY COALESCE(codigo_documento_base, codigo_documento) DESC, version DESC, created_at DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", codigoInspeccion);
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(Map(dr));
                        }
                    }
                }
            }

            return lista;
        }

        public DocumentoInspeccion ObtenerPorId(int codigoDocumento)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT codigo_documento,
                           codigo_inspeccion,
                           codigo_informe,
                           codigo_documento_base,
                           version,
                           tipo_documento,
                           nombre_archivo_original,
                           nombre_archivo_storage,
                           ruta_archivo,
                           hash_archivo,
                           tamano_bytes,
                           content_type,
                           observacion,
                           subido_por_rol,
                           codigo_usuario,
                           created_at
                    FROM public.aocr_tbdocumento_inspeccion
                    WHERE codigo_documento = @codigo_documento
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_documento", codigoDocumento);
                    using (var dr = cmd.ExecuteReader())
                    {
                        return dr.Read() ? Map(dr) : null;
                    }
                }
            }
        }

        public DocumentoInspeccion RegistrarDocumentoVersionado(DocumentoInspeccion documento)
        {
            if (documento == null)
            {
                throw new ArgumentNullException(nameof(documento));
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                var codigoBase = documento.CodigoDocumentoBase;
                if (codigoBase.HasValue)
                {
                    var documentoBase = ObtenerPorIdInterno(cn, codigoBase.Value);
                    if (documentoBase != null && documentoBase.CodigoDocumentoBase.HasValue)
                    {
                        codigoBase = documentoBase.CodigoDocumentoBase.Value;
                    }
                }

                var version = ObtenerSiguienteVersion(cn, codigoBase, documento.CodigoInspeccion);

                const string sql = @"
                    INSERT INTO public.aocr_tbdocumento_inspeccion
                    (
                        codigo_inspeccion,
                        codigo_informe,
                        codigo_documento_base,
                        version,
                        tipo_documento,
                        nombre_archivo_original,
                        nombre_archivo_storage,
                        ruta_archivo,
                        hash_archivo,
                        tamano_bytes,
                        content_type,
                        observacion,
                        subido_por_rol,
                        codigo_usuario,
                        created_at
                    )
                    VALUES
                    (
                        @codigo_inspeccion,
                        @codigo_informe,
                        @codigo_documento_base,
                        @version,
                        @tipo_documento,
                        @nombre_archivo_original,
                        @nombre_archivo_storage,
                        @ruta_archivo,
                        @hash_archivo,
                        @tamano_bytes,
                        @content_type,
                        @observacion,
                        @subido_por_rol,
                        @codigo_usuario,
                        NOW()
                    )
                    RETURNING codigo_documento;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", documento.CodigoInspeccion);
                    cmd.Parameters.AddWithValue("@codigo_informe", (object)documento.CodigoInforme ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_documento_base", (object)codigoBase ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@version", version);
                    cmd.Parameters.AddWithValue("@tipo_documento", (object)documento.TipoDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nombre_archivo_original", (object)documento.NombreArchivoOriginal ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nombre_archivo_storage", (object)documento.NombreArchivoStorage ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_archivo", (object)documento.RutaArchivo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hash_archivo", (object)documento.HashArchivo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tamano_bytes", (object)documento.TamanoBytes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@content_type", (object)documento.ContentType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observacion", (object)documento.Observacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@subido_por_rol", (object)documento.SubidoPorRol ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_usuario", (object)documento.CodigoUsuario ?? DBNull.Value);
                    var codigoDocumento = Convert.ToInt32(cmd.ExecuteScalar());
                    return ObtenerPorIdInterno(cn, codigoDocumento);
                }
            }
        }

        private static DocumentoInspeccion Map(NpgsqlDataReader dr)
        {
            return new DocumentoInspeccion
            {
                CodigoDocumento = dr.GetInt32(dr.GetOrdinal("codigo_documento")),
                CodigoInspeccion = dr.GetInt32(dr.GetOrdinal("codigo_inspeccion")),
                CodigoInforme = dr["codigo_informe"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["codigo_informe"]),
                CodigoDocumentoBase = dr["codigo_documento_base"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["codigo_documento_base"]),
                Version = dr["version"] == DBNull.Value ? 1 : Convert.ToInt32(dr["version"]),
                TipoDocumento = dr["tipo_documento"] == DBNull.Value ? null : dr["tipo_documento"].ToString(),
                NombreArchivoOriginal = dr["nombre_archivo_original"] == DBNull.Value ? null : dr["nombre_archivo_original"].ToString(),
                NombreArchivoStorage = dr["nombre_archivo_storage"] == DBNull.Value ? null : dr["nombre_archivo_storage"].ToString(),
                RutaArchivo = dr["ruta_archivo"] == DBNull.Value ? null : dr["ruta_archivo"].ToString(),
                HashArchivo = dr["hash_archivo"] == DBNull.Value ? null : dr["hash_archivo"].ToString(),
                TamanoBytes = dr["tamano_bytes"] == DBNull.Value ? null : (long?)Convert.ToInt64(dr["tamano_bytes"]),
                ContentType = dr["content_type"] == DBNull.Value ? null : dr["content_type"].ToString(),
                Observacion = dr["observacion"] == DBNull.Value ? null : dr["observacion"].ToString(),
                SubidoPorRol = dr["subido_por_rol"] == DBNull.Value ? null : dr["subido_por_rol"].ToString(),
                CodigoUsuario = dr["codigo_usuario"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["codigo_usuario"]),
                CreatedAt = dr["created_at"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(dr["created_at"])
            };
        }

        private static void EnsureSchema(NpgsqlConnection cn)
        {
            if (_schemaReady)
            {
                return;
            }

            lock (SyncLock)
            {
                if (_schemaReady)
                {
                    return;
                }

                const string sql = @"
                    CREATE TABLE IF NOT EXISTS public.aocr_tbdocumento_inspeccion
                    (
                        codigo_documento SERIAL PRIMARY KEY,
                        codigo_inspeccion INTEGER NOT NULL,
                        codigo_informe INTEGER,
                        codigo_documento_base INTEGER,
                        version INTEGER NOT NULL DEFAULT 1,
                        tipo_documento VARCHAR(150),
                        nombre_archivo_original VARCHAR(255),
                        nombre_archivo_storage VARCHAR(255),
                        ruta_archivo VARCHAR(600) NOT NULL,
                        hash_archivo VARCHAR(64),
                        tamano_bytes BIGINT,
                        content_type VARCHAR(150),
                        observacion TEXT,
                        subido_por_rol VARCHAR(60),
                        codigo_usuario INTEGER,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW()
                    );

                    CREATE INDEX IF NOT EXISTS idx_documento_inspeccion_codigo
                        ON public.aocr_tbdocumento_inspeccion(codigo_inspeccion, created_at DESC);

                    CREATE INDEX IF NOT EXISTS idx_documento_inspeccion_base
                        ON public.aocr_tbdocumento_inspeccion(codigo_documento_base, version DESC);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                const string alterSql = @"
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS codigo_informe INTEGER;
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS codigo_documento_base INTEGER;
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS version INTEGER NOT NULL DEFAULT 1;
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS tipo_documento VARCHAR(150);
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS nombre_archivo_original VARCHAR(255);
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS nombre_archivo_storage VARCHAR(255);
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS ruta_archivo VARCHAR(600);
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS hash_archivo VARCHAR(64);
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS tamano_bytes BIGINT;
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS content_type VARCHAR(150);
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS observacion TEXT;
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS subido_por_rol VARCHAR(60);
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS codigo_usuario INTEGER;
                    ALTER TABLE public.aocr_tbdocumento_inspeccion ADD COLUMN IF NOT EXISTS created_at TIMESTAMP NOT NULL DEFAULT NOW();";

                using (var cmd = new NpgsqlCommand(alterSql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }

        private static int ObtenerSiguienteVersion(NpgsqlConnection cn, int? codigoDocumentoBase, int codigoInspeccion)
        {
            if (!codigoDocumentoBase.HasValue)
            {
                return 1;
            }

            const string sql = @"
                SELECT COALESCE(MAX(version), 0)
                FROM public.aocr_tbdocumento_inspeccion
                WHERE codigo_inspeccion = @codigo_inspeccion
                  AND (codigo_documento = @codigo_documento_base OR codigo_documento_base = @codigo_documento_base);";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_inspeccion", codigoInspeccion);
                cmd.Parameters.AddWithValue("@codigo_documento_base", codigoDocumentoBase.Value);
                return Convert.ToInt32(cmd.ExecuteScalar()) + 1;
            }
        }

        private static DocumentoInspeccion ObtenerPorIdInterno(NpgsqlConnection cn, int codigoDocumento)
        {
            const string sql = @"
                SELECT codigo_documento,
                       codigo_inspeccion,
                       codigo_informe,
                       codigo_documento_base,
                       version,
                       tipo_documento,
                       nombre_archivo_original,
                       nombre_archivo_storage,
                       ruta_archivo,
                       hash_archivo,
                       tamano_bytes,
                       content_type,
                       observacion,
                       subido_por_rol,
                       codigo_usuario,
                       created_at
                FROM public.aocr_tbdocumento_inspeccion
                WHERE codigo_documento = @codigo_documento
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_documento", codigoDocumento);
                using (var dr = cmd.ExecuteReader())
                {
                    return dr.Read() ? Map(dr) : null;
                }
            }
        }
    }
}