using System;
using System.Configuration;
using CapaModelo;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class AocrDocumentoGeneradoDAO
    {
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;
        private readonly string _cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;

        public void Registrar(AocrDocumentoGenerado documento)
        {
            if (documento == null)
            {
                throw new ArgumentNullException(nameof(documento));
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    WITH guard AS (
                        SELECT pg_advisory_xact_lock(@codigo_solicitud::bigint)
                    ), actual AS (
                        SELECT codigo_documento
                        FROM public.aocr_tbdocumento_generado, guard
                        WHERE codigo_solicitud = @codigo_solicitud
                          AND UPPER(tipo_documento) = UPPER(@tipo_documento)
                          AND vigente = TRUE
                        FOR UPDATE
                    ), misma_evidencia AS (
                        SELECT codigo_documento
                        FROM public.aocr_tbdocumento_generado
                        WHERE codigo_documento IN (SELECT codigo_documento FROM actual)
                          AND NULLIF(hash_pdf, '') IS NOT DISTINCT FROM NULLIF(@hash_pdf, '')
                    ), anterior AS (
                        UPDATE public.aocr_tbdocumento_generado
                           SET vigente = FALSE,
                               estado = CASE WHEN bloqueado THEN estado ELSE 'VERSION_ANTERIOR' END,
                               version_concurrencia = version_concurrencia + 1
                         WHERE codigo_documento IN (SELECT codigo_documento FROM actual)
                           AND NOT EXISTS (SELECT 1 FROM misma_evidencia)
                        RETURNING codigo_documento
                    )
                    INSERT INTO public.aocr_tbdocumento_generado
                    (
                        codigo_solicitud,
                        codigo_inspeccion,
                        tipo_documento,
                        numero_aocr,
                        nombre_archivo,
                        ruta_documento,
                        tamanio_pdf,
                        estado,
                        fecha_generacion,
                        codigo_usuario,
                        usuario_nombre,
                        version_documento,
                        vigente,
                        completo,
                        bloqueado,
                        hash_pdf,
                        version_concurrencia,
                        created_at
                    )
                    SELECT
                        @codigo_solicitud,
                        @codigo_inspeccion,
                        @tipo_documento,
                        @numero_aocr,
                        @nombre_archivo,
                        @ruta_documento,
                        @tamanio_pdf,
                        @estado,
                        @fecha_generacion,
                        @codigo_usuario,
                        @usuario_nombre,
                        COALESCE((SELECT MAX(version_documento) + 1 FROM public.aocr_tbdocumento_generado WHERE codigo_solicitud=@codigo_solicitud AND UPPER(tipo_documento)=UPPER(@tipo_documento)), 1),
                        TRUE,
                        @completo,
                        FALSE,
                        @hash_pdf,
                        1,
                        NOW()
                    WHERE NOT EXISTS (SELECT 1 FROM misma_evidencia);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", documento.CodigoSolicitud);
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", (object)documento.CodigoInspeccion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo_documento", (object)documento.TipoDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@numero_aocr", (object)documento.NumeroAocr ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nombre_archivo", (object)documento.NombreArchivo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_documento", (object)documento.RutaDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tamanio_pdf", (object)documento.TamanioPdf ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado", (object)documento.Estado ?? "GENERADO");
                    cmd.Parameters.AddWithValue("@fecha_generacion", documento.FechaGeneracion == DateTime.MinValue ? DateTime.Now : documento.FechaGeneracion);
                    cmd.Parameters.AddWithValue("@codigo_usuario", (object)documento.CodigoUsuario ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_nombre", (object)documento.UsuarioNombre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@completo", documento.Completo);
                    cmd.Parameters.AddWithValue("@hash_pdf", (object)documento.HashPdf ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public AocrDocumentoGenerado ObtenerUltimoPorSolicitudTipo(int codigoSolicitud, string tipoDocumento)
        {
            if (codigoSolicitud <= 0 || string.IsNullOrWhiteSpace(tipoDocumento))
            {
                return null;
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT codigo_documento,
                           codigo_solicitud,
                           codigo_inspeccion,
                           tipo_documento,
                           numero_aocr,
                           nombre_archivo,
                           ruta_documento,
                           tamanio_pdf,
                           estado,
                           fecha_generacion,
                           codigo_usuario,
                           usuario_nombre,
                           created_at,
                           version_documento,
                           vigente,
                           completo,
                           bloqueado,
                           hash_pdf,
                           ruta_pdf_firmado,
                           hash_pdf_firmado,
                           tamanio_pdf_firmado,
                           codigo_usuario_firma,
                           rol_firma,
                           fecha_firma,
                           version_concurrencia
                    FROM public.aocr_tbdocumento_generado
                    WHERE codigo_solicitud = @codigo_solicitud
                      AND UPPER(COALESCE(tipo_documento, '')) = UPPER(@tipo_documento)
                      AND vigente = TRUE
                    ORDER BY version_documento DESC, codigo_documento DESC
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@tipo_documento", tipoDocumento.Trim());
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            return null;
                        }

                        return new AocrDocumentoGenerado
                        {
                            CodigoDocumento = rd["codigo_documento"] != DBNull.Value ? Convert.ToInt32(rd["codigo_documento"]) : 0,
                            CodigoSolicitud = rd["codigo_solicitud"] != DBNull.Value ? Convert.ToInt32(rd["codigo_solicitud"]) : 0,
                            CodigoInspeccion = rd["codigo_inspeccion"] != DBNull.Value ? (int?)Convert.ToInt32(rd["codigo_inspeccion"]) : null,
                            TipoDocumento = rd["tipo_documento"] != DBNull.Value ? rd["tipo_documento"].ToString() : null,
                            NumeroAocr = rd["numero_aocr"] != DBNull.Value ? rd["numero_aocr"].ToString() : null,
                            NombreArchivo = rd["nombre_archivo"] != DBNull.Value ? rd["nombre_archivo"].ToString() : null,
                            RutaDocumento = rd["ruta_documento"] != DBNull.Value ? rd["ruta_documento"].ToString() : null,
                            TamanioPdf = rd["tamanio_pdf"] != DBNull.Value ? (long?)Convert.ToInt64(rd["tamanio_pdf"]) : null,
                            Estado = rd["estado"] != DBNull.Value ? rd["estado"].ToString() : null,
                            FechaGeneracion = rd["fecha_generacion"] != DBNull.Value ? Convert.ToDateTime(rd["fecha_generacion"]) : DateTime.MinValue,
                            CodigoUsuario = rd["codigo_usuario"] != DBNull.Value ? (int?)Convert.ToInt32(rd["codigo_usuario"]) : null,
                            UsuarioNombre = rd["usuario_nombre"] != DBNull.Value ? rd["usuario_nombre"].ToString() : null,
                            CreatedAt = rd["created_at"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["created_at"]) : null,
                            VersionDocumento = rd["version_documento"] != DBNull.Value ? Convert.ToInt32(rd["version_documento"]) : 1,
                            Vigente = rd["vigente"] != DBNull.Value && Convert.ToBoolean(rd["vigente"]),
                            Completo = rd["completo"] != DBNull.Value && Convert.ToBoolean(rd["completo"]),
                            Bloqueado = rd["bloqueado"] != DBNull.Value && Convert.ToBoolean(rd["bloqueado"]),
                            HashPdf = rd["hash_pdf"] != DBNull.Value ? rd["hash_pdf"].ToString() : null,
                            RutaPdfFirmado = rd["ruta_pdf_firmado"] != DBNull.Value ? rd["ruta_pdf_firmado"].ToString() : null,
                            HashPdfFirmado = rd["hash_pdf_firmado"] != DBNull.Value ? rd["hash_pdf_firmado"].ToString() : null,
                            TamanioPdfFirmado = rd["tamanio_pdf_firmado"] != DBNull.Value ? (long?)Convert.ToInt64(rd["tamanio_pdf_firmado"]) : null,
                            CodigoUsuarioFirma = rd["codigo_usuario_firma"] != DBNull.Value ? (int?)Convert.ToInt32(rd["codigo_usuario_firma"]) : null,
                            RolFirma = rd["rol_firma"] != DBNull.Value ? rd["rol_firma"].ToString() : null,
                            FechaFirma = rd["fecha_firma"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["fecha_firma"]) : null,
                            VersionConcurrencia = rd["version_concurrencia"] != DBNull.Value ? Convert.ToInt64(rd["version_concurrencia"]) : 1L
                        };
                    }
                }
            }
        }

        public void MarcarLiberadoRt(
            int codigoSolicitud,
            string tipoDocumento,
            string rutaPdfFirmado,
            string hashPdfFirmado,
            long tamanioPdf,
            int? codigoUsuarioLiberacion,
            string nombreArchivo = null)
        {
            if (codigoSolicitud <= 0 || string.IsNullOrWhiteSpace(tipoDocumento) || string.IsNullOrWhiteSpace(rutaPdfFirmado))
            {
                return;
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sqlUpdate = @"
                    UPDATE public.aocr_tbdocumento_generado
                    SET ruta_documento = @ruta_documento,
                        nombre_archivo = COALESCE(NULLIF(@nombre_archivo, ''), nombre_archivo),
                        tamanio_pdf = @tamanio_pdf,
                        estado = 'LIBERADO_RT',
                        hash_pdf_firmado = @hash_pdf_firmado,
                        fecha_liberacion = COALESCE(fecha_liberacion, NOW()),
                        disponible_rt = TRUE,
                        fecha_disponible_rt = COALESCE(fecha_disponible_rt, NOW()),
                        codigo_usuario_liberacion = @codigo_usuario_liberacion
                    WHERE codigo_documento = (
                        SELECT codigo_documento
                        FROM public.aocr_tbdocumento_generado
                        WHERE codigo_solicitud = @codigo_solicitud
                          AND UPPER(COALESCE(tipo_documento, '')) = UPPER(@tipo_documento)
                        ORDER BY fecha_generacion DESC, codigo_documento DESC
                        LIMIT 1
                    );";

                int filas;
                using (var cmd = new NpgsqlCommand(sqlUpdate, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@tipo_documento", tipoDocumento.Trim());
                    cmd.Parameters.AddWithValue("@ruta_documento", rutaPdfFirmado.Trim());
                    cmd.Parameters.AddWithValue("@nombre_archivo", (object)(nombreArchivo ?? string.Empty));
                    cmd.Parameters.AddWithValue("@tamanio_pdf", tamanioPdf);
                    cmd.Parameters.AddWithValue("@hash_pdf_firmado", (object)hashPdfFirmado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_usuario_liberacion", (object)codigoUsuarioLiberacion ?? DBNull.Value);
                    filas = cmd.ExecuteNonQuery();
                }

                if (filas > 0)
                {
                    return;
                }

                const string sqlInsert = @"
                    INSERT INTO public.aocr_tbdocumento_generado
                    (
                        codigo_solicitud,
                        tipo_documento,
                        nombre_archivo,
                        ruta_documento,
                        tamanio_pdf,
                        estado,
                        fecha_generacion,
                        hash_pdf_firmado,
                        fecha_liberacion,
                        disponible_rt,
                        fecha_disponible_rt,
                        codigo_usuario,
                        codigo_usuario_liberacion,
                        created_at
                    )
                    VALUES
                    (
                        @codigo_solicitud,
                        @tipo_documento,
                        @nombre_archivo,
                        @ruta_documento,
                        @tamanio_pdf,
                        'LIBERADO_RT',
                        NOW(),
                        @hash_pdf_firmado,
                        NOW(),
                        TRUE,
                        NOW(),
                        @codigo_usuario,
                        @codigo_usuario_liberacion,
                        NOW()
                    );";

                using (var cmd = new NpgsqlCommand(sqlInsert, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@tipo_documento", tipoDocumento.Trim());
                    cmd.Parameters.AddWithValue("@nombre_archivo", (object)nombreArchivo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_documento", rutaPdfFirmado.Trim());
                    cmd.Parameters.AddWithValue("@tamanio_pdf", tamanioPdf);
                    cmd.Parameters.AddWithValue("@hash_pdf_firmado", (object)hashPdfFirmado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_usuario", (object)codigoUsuarioLiberacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_usuario_liberacion", (object)codigoUsuarioLiberacion ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
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
                    CREATE TABLE IF NOT EXISTS public.aocr_tbdocumento_generado
                    (
                        codigo_documento SERIAL PRIMARY KEY,
                        codigo_solicitud INTEGER NOT NULL,
                        codigo_inspeccion INTEGER NULL,
                        tipo_documento VARCHAR(80) NOT NULL,
                        numero_aocr VARCHAR(120) NULL,
                        nombre_archivo VARCHAR(260) NULL,
                        ruta_documento VARCHAR(500) NOT NULL,
                        tamanio_pdf BIGINT NULL,
                        estado VARCHAR(80) NOT NULL DEFAULT 'GENERADO',
                        fecha_generacion TIMESTAMP NOT NULL DEFAULT NOW(),
                        codigo_usuario INTEGER NULL,
                        usuario_nombre VARCHAR(160) NULL,
                        hash_pdf_firmado VARCHAR(128) NULL,
                        fecha_liberacion TIMESTAMP NULL,
                        codigo_usuario_liberacion INTEGER NULL,
                        disponible_rt BOOLEAN NOT NULL DEFAULT FALSE,
                        fecha_disponible_rt TIMESTAMP NULL,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW()
                    );

                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS hash_pdf_firmado VARCHAR(128) NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS fecha_liberacion TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS codigo_usuario_liberacion INTEGER NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS disponible_rt BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS fecha_disponible_rt TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS version_documento INTEGER NOT NULL DEFAULT 1;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS vigente BOOLEAN NOT NULL DEFAULT TRUE;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS completo BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS bloqueado BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS hash_pdf VARCHAR(128) NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS ruta_pdf_firmado VARCHAR(500) NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS tamanio_pdf_firmado BIGINT NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS codigo_usuario_firma INTEGER NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS rol_firma VARCHAR(100) NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS fecha_firma TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS version_concurrencia BIGINT NOT NULL DEFAULT 1;

                    CREATE INDEX IF NOT EXISTS idx_aocr_documento_generado_solicitud_tipo
                        ON public.aocr_tbdocumento_generado(codigo_solicitud, tipo_documento, fecha_generacion DESC);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }
    }
}
