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
