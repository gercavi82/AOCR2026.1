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
                        created_at
                    )
                    VALUES
                    (
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
                        NOW()
                    );";

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
                           created_at
                    FROM public.aocr_tbdocumento_generado
                    WHERE codigo_solicitud = @codigo_solicitud
                      AND UPPER(COALESCE(tipo_documento, '')) = UPPER(@tipo_documento)
                    ORDER BY fecha_generacion DESC, codigo_documento DESC
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
                            CreatedAt = rd["created_at"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["created_at"]) : null
                        };
                    }
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
                        created_at TIMESTAMP NOT NULL DEFAULT NOW()
                    );

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
