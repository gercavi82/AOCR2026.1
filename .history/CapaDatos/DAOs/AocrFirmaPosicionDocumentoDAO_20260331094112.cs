using System;
using System.Configuration;
using CapaModelo;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class AocrFirmaPosicionDocumentoDAO
    {
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;

        private readonly string _cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;

        public AocrFirmaPosicionDocumento Obtener(int codigoSolicitud, string tipoDocumento, string rolFirmante)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT
                        codigo_posicion_firma,
                        codigo_solicitud,
                        codigo_inspeccion,
                        tipo_documento,
                        rol_firmante,
                        origen_posicion,
                        numero_pagina,
                        posicion_x_ratio,
                        posicion_y_ratio,
                        ancho_ratio,
                        alto_ratio,
                        codigo_usuario,
                        usuario_nombre,
                        created_at,
                        updated_at
                    FROM public.aocr_tbfirma_posicion_documento
                    WHERE codigo_solicitud = @codigo_solicitud
                      AND UPPER(tipo_documento) = UPPER(@tipo_documento)
                      AND UPPER(rol_firmante) = UPPER(@rol_firmante)
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@tipo_documento", (object)(tipoDocumento ?? string.Empty));
                    cmd.Parameters.AddWithValue("@rol_firmante", (object)(rolFirmante ?? string.Empty));

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new AocrFirmaPosicionDocumento
                        {
                            CodigoPosicionFirma = reader.GetInt32(reader.GetOrdinal("codigo_posicion_firma")),
                            CodigoSolicitud = reader.GetInt32(reader.GetOrdinal("codigo_solicitud")),
                            CodigoInspeccion = reader.IsDBNull(reader.GetOrdinal("codigo_inspeccion")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("codigo_inspeccion")),
                            TipoDocumento = reader.IsDBNull(reader.GetOrdinal("tipo_documento")) ? null : reader.GetString(reader.GetOrdinal("tipo_documento")),
                            RolFirmante = reader.IsDBNull(reader.GetOrdinal("rol_firmante")) ? null : reader.GetString(reader.GetOrdinal("rol_firmante")),
                            OrigenPosicion = reader.IsDBNull(reader.GetOrdinal("origen_posicion")) ? null : reader.GetString(reader.GetOrdinal("origen_posicion")),
                            NumeroPagina = reader.IsDBNull(reader.GetOrdinal("numero_pagina")) ? 1 : reader.GetInt32(reader.GetOrdinal("numero_pagina")),
                            PosicionXRatio = reader.IsDBNull(reader.GetOrdinal("posicion_x_ratio")) ? 0m : reader.GetDecimal(reader.GetOrdinal("posicion_x_ratio")),
                            PosicionYRatio = reader.IsDBNull(reader.GetOrdinal("posicion_y_ratio")) ? 0m : reader.GetDecimal(reader.GetOrdinal("posicion_y_ratio")),
                            AnchoRatio = reader.IsDBNull(reader.GetOrdinal("ancho_ratio")) ? 0m : reader.GetDecimal(reader.GetOrdinal("ancho_ratio")),
                            AltoRatio = reader.IsDBNull(reader.GetOrdinal("alto_ratio")) ? 0m : reader.GetDecimal(reader.GetOrdinal("alto_ratio")),
                            CodigoUsuario = reader.IsDBNull(reader.GetOrdinal("codigo_usuario")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("codigo_usuario")),
                            UsuarioNombre = reader.IsDBNull(reader.GetOrdinal("usuario_nombre")) ? null : reader.GetString(reader.GetOrdinal("usuario_nombre")),
                            CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("created_at")),
                            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("updated_at"))
                        };
                    }
                }
            }
        }

        public void Guardar(AocrFirmaPosicionDocumento posicion)
        {
            if (posicion == null)
            {
                throw new ArgumentNullException(nameof(posicion));
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    INSERT INTO public.aocr_tbfirma_posicion_documento
                    (
                        codigo_solicitud,
                        codigo_inspeccion,
                        tipo_documento,
                        rol_firmante,
                        origen_posicion,
                        numero_pagina,
                        posicion_x_ratio,
                        posicion_y_ratio,
                        ancho_ratio,
                        alto_ratio,
                        codigo_usuario,
                        usuario_nombre,
                        created_at,
                        updated_at
                    )
                    VALUES
                    (
                        @codigo_solicitud,
                        @codigo_inspeccion,
                        @tipo_documento,
                        @rol_firmante,
                        @origen_posicion,
                        @numero_pagina,
                        @posicion_x_ratio,
                        @posicion_y_ratio,
                        @ancho_ratio,
                        @alto_ratio,
                        @codigo_usuario,
                        @usuario_nombre,
                        NOW(),
                        NOW()
                    )
                    ON CONFLICT (codigo_solicitud, tipo_documento, rol_firmante)
                    DO UPDATE SET
                        codigo_inspeccion = EXCLUDED.codigo_inspeccion,
                        origen_posicion = EXCLUDED.origen_posicion,
                        numero_pagina = EXCLUDED.numero_pagina,
                        posicion_x_ratio = EXCLUDED.posicion_x_ratio,
                        posicion_y_ratio = EXCLUDED.posicion_y_ratio,
                        ancho_ratio = EXCLUDED.ancho_ratio,
                        alto_ratio = EXCLUDED.alto_ratio,
                        codigo_usuario = EXCLUDED.codigo_usuario,
                        usuario_nombre = EXCLUDED.usuario_nombre,
                        updated_at = NOW();";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", posicion.CodigoSolicitud);
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", (object)posicion.CodigoInspeccion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo_documento", (object)posicion.TipoDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@rol_firmante", (object)posicion.RolFirmante ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@origen_posicion", (object)posicion.OrigenPosicion ?? "PUNTERO");
                    cmd.Parameters.AddWithValue("@numero_pagina", posicion.NumeroPagina <= 0 ? 1 : posicion.NumeroPagina);
                    cmd.Parameters.AddWithValue("@posicion_x_ratio", posicion.PosicionXRatio);
                    cmd.Parameters.AddWithValue("@posicion_y_ratio", posicion.PosicionYRatio);
                    cmd.Parameters.AddWithValue("@ancho_ratio", posicion.AnchoRatio);
                    cmd.Parameters.AddWithValue("@alto_ratio", posicion.AltoRatio);
                    cmd.Parameters.AddWithValue("@codigo_usuario", (object)posicion.CodigoUsuario ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_nombre", (object)posicion.UsuarioNombre ?? DBNull.Value);
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
                    CREATE TABLE IF NOT EXISTS public.aocr_tbfirma_posicion_documento
                    (
                        codigo_posicion_firma SERIAL PRIMARY KEY,
                        codigo_solicitud INTEGER NOT NULL,
                        codigo_inspeccion INTEGER NULL,
                        tipo_documento VARCHAR(80) NOT NULL,
                        rol_firmante VARCHAR(80) NOT NULL,
                        origen_posicion VARCHAR(30) NOT NULL DEFAULT 'PUNTERO',
                        numero_pagina INTEGER NOT NULL DEFAULT 1,
                        posicion_x_ratio NUMERIC(10,6) NOT NULL,
                        posicion_y_ratio NUMERIC(10,6) NOT NULL,
                        ancho_ratio NUMERIC(10,6) NOT NULL,
                        alto_ratio NUMERIC(10,6) NOT NULL,
                        codigo_usuario INTEGER NULL,
                        usuario_nombre VARCHAR(160) NULL,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        updated_at TIMESTAMP NOT NULL DEFAULT NOW()
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_firma_posicion_documento
                        ON public.aocr_tbfirma_posicion_documento(codigo_solicitud, tipo_documento, rol_firmante);

                    CREATE INDEX IF NOT EXISTS idx_aocr_firma_posicion_documento_solicitud
                        ON public.aocr_tbfirma_posicion_documento(codigo_solicitud, updated_at DESC);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }
    }
}