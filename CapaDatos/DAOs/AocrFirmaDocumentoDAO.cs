using System;
using System.Configuration;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class AocrFirmaDocumentoDAO
    {
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;

        private readonly string _cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;

        public void Registrar(AocrFirmaDocumento firma)
        {
            if (firma == null)
            {
                throw new ArgumentNullException(nameof(firma));
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    INSERT INTO public.aocr_tbfirma_documento
                    (
                        codigo_solicitud,
                        codigo_inspeccion,
                        tipo_documento,
                        numero_aocr,
                        nombre_archivo,
                        ruta_documento,
                        hash_documento,
                        codigo_qr,
                        sujeto_certificado,
                        nombre_firmante,
                        cargo_firmante,
                        fecha_firma,
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
                        @hash_documento,
                        @codigo_qr,
                        @sujeto_certificado,
                        @nombre_firmante,
                        @cargo_firmante,
                        @fecha_firma,
                        @codigo_usuario,
                        @usuario_nombre,
                        NOW()
                    );";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", firma.CodigoSolicitud);
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", (object)firma.CodigoInspeccion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo_documento", (object)firma.TipoDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@numero_aocr", (object)firma.NumeroAocr ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nombre_archivo", (object)firma.NombreArchivo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_documento", (object)firma.RutaDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hash_documento", (object)firma.HashDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_qr", (object)firma.CodigoQr ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@sujeto_certificado", (object)firma.SujetoCertificado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nombre_firmante", (object)firma.NombreFirmante ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cargo_firmante", (object)firma.CargoFirmante ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma", firma.FechaFirma);
                    cmd.Parameters.AddWithValue("@codigo_usuario", (object)firma.CodigoUsuario ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_nombre", (object)firma.UsuarioNombre ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public AocrFirmaDocumento ObtenerUltimoPorSolicitudTipo(int codigoSolicitud, string tipoDocumento)
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
                    SELECT codigo_firma,
                           codigo_solicitud,
                           codigo_inspeccion,
                           tipo_documento,
                           numero_aocr,
                           nombre_archivo,
                           ruta_documento,
                           hash_documento,
                           codigo_qr,
                           sujeto_certificado,
                           nombre_firmante,
                           cargo_firmante,
                           fecha_firma,
                           codigo_usuario,
                           usuario_nombre,
                           created_at
                    FROM public.aocr_tbfirma_documento
                    WHERE codigo_solicitud = @codigo_solicitud
                      AND UPPER(COALESCE(tipo_documento, '')) = UPPER(@tipo_documento)
                    ORDER BY fecha_firma DESC, codigo_firma DESC
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

                        return new AocrFirmaDocumento
                        {
                            CodigoFirma = rd["codigo_firma"] != DBNull.Value ? Convert.ToInt32(rd["codigo_firma"]) : 0,
                            CodigoSolicitud = rd["codigo_solicitud"] != DBNull.Value ? Convert.ToInt32(rd["codigo_solicitud"]) : 0,
                            CodigoInspeccion = rd["codigo_inspeccion"] != DBNull.Value ? (int?)Convert.ToInt32(rd["codigo_inspeccion"]) : null,
                            TipoDocumento = rd["tipo_documento"] != DBNull.Value ? rd["tipo_documento"].ToString() : null,
                            NumeroAocr = rd["numero_aocr"] != DBNull.Value ? rd["numero_aocr"].ToString() : null,
                            NombreArchivo = rd["nombre_archivo"] != DBNull.Value ? rd["nombre_archivo"].ToString() : null,
                            RutaDocumento = rd["ruta_documento"] != DBNull.Value ? rd["ruta_documento"].ToString() : null,
                            HashDocumento = rd["hash_documento"] != DBNull.Value ? rd["hash_documento"].ToString() : null,
                            CodigoQr = rd["codigo_qr"] != DBNull.Value ? rd["codigo_qr"].ToString() : null,
                            SujetoCertificado = rd["sujeto_certificado"] != DBNull.Value ? rd["sujeto_certificado"].ToString() : null,
                            NombreFirmante = rd["nombre_firmante"] != DBNull.Value ? rd["nombre_firmante"].ToString() : null,
                            CargoFirmante = rd["cargo_firmante"] != DBNull.Value ? rd["cargo_firmante"].ToString() : null,
                            FechaFirma = rd["fecha_firma"] != DBNull.Value ? Convert.ToDateTime(rd["fecha_firma"]) : DateTime.MinValue,
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
                    CREATE TABLE IF NOT EXISTS public.aocr_tbfirma_documento
                    (
                        codigo_firma SERIAL PRIMARY KEY,
                        codigo_solicitud INTEGER NOT NULL,
                        codigo_inspeccion INTEGER NULL,
                        tipo_documento VARCHAR(80) NOT NULL,
                        numero_aocr VARCHAR(120) NULL,
                        nombre_archivo VARCHAR(260) NULL,
                        ruta_documento VARCHAR(500) NULL,
                        hash_documento VARCHAR(256) NULL,
                        codigo_qr TEXT NULL,
                        sujeto_certificado TEXT NULL,
                        nombre_firmante VARCHAR(250) NULL,
                        cargo_firmante VARCHAR(250) NULL,
                        fecha_firma TIMESTAMP NOT NULL,
                        codigo_usuario INTEGER NULL,
                        usuario_nombre VARCHAR(160) NULL,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW()
                    );

                    CREATE INDEX IF NOT EXISTS idx_aocr_firma_documento_solicitud
                        ON public.aocr_tbfirma_documento(codigo_solicitud, fecha_firma DESC);

                    CREATE INDEX IF NOT EXISTS idx_aocr_firma_documento_hash
                        ON public.aocr_tbfirma_documento(hash_documento);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }
    }
}
