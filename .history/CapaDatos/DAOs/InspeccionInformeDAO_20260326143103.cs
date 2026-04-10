using System;
using System.Configuration;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class InspeccionInformeDAO
    {
        private readonly string _cs;
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;

        public InspeccionInformeDAO()
        {
            var settings = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _cs = (settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString))
                ? settings.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public InspeccionInformeTecnico ObtenerUltimoPorInspeccion(int codigoInspeccion)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT codigo_informe,
                           codigo_inspeccion,
                           version,
                           titulo,
                           resumen,
                           resultado,
                           observaciones,
                           conclusiones,
                           recomendaciones,
                           ruta_pdf,
                              estado_informe,
                              firmado_inspector,
                              firmado_dirdac,
                              ruta_documento_firmado,
                              hash_documento,
                              fecha_firma_1,
                              fecha_firma_2,
                              usuario_firma_1,
                              usuario_firma_2,
                              fecha_envio_dirdac,
                              usuario_envio_dirdac,
                           finalizado,
                           correo_enviado,
                           fecha_finalizacion,
                           created_at,
                           created_by,
                           updated_at,
                           updated_by
                    FROM public.aocr_tbinforme_inspeccion
                    WHERE codigo_inspeccion = @codigo_inspeccion
                    ORDER BY version DESC
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", codigoInspeccion);
                    using (var dr = cmd.ExecuteReader())
                    {
                        return dr.Read() ? Map(dr) : null;
                    }
                }
            }
        }

        public InspeccionInformeTecnico GuardarBorrador(InspeccionInformeTecnico informe, int usuarioId)
        {
            if (informe == null)
            {
                throw new ArgumentNullException(nameof(informe));
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                var ultimo = ObtenerUltimoPorInspeccionInterno(cn, informe.CodigoInspeccion);
                if (ultimo != null && !ultimo.Finalizado)
                {
                    const string updateSql = @"
                        UPDATE public.aocr_tbinforme_inspeccion
                        SET titulo = @titulo,
                            resumen = @resumen,
                            resultado = @resultado,
                            observaciones = @observaciones,
                            conclusiones = @conclusiones,
                            recomendaciones = @recomendaciones,
                            updated_at = NOW(),
                            updated_by = @updated_by
                        WHERE codigo_informe = @codigo_informe;";

                    using (var cmd = new NpgsqlCommand(updateSql, cn))
                    {
                        Bind(cmd, informe, usuarioId);
                        cmd.Parameters.AddWithValue("@codigo_informe", ultimo.CodigoInforme);
                        cmd.ExecuteNonQuery();
                    }

                    return ObtenerPorIdInterno(cn, ultimo.CodigoInforme);
                }

                var version = ultimo != null ? ultimo.Version + 1 : 1;
                const string insertSql = @"
                    INSERT INTO public.aocr_tbinforme_inspeccion
                    (
                        codigo_inspeccion,
                        version,
                        titulo,
                        resumen,
                        resultado,
                        observaciones,
                        conclusiones,
                        recomendaciones,
                        finalizado,
                        correo_enviado,
                        created_at,
                        created_by,
                        updated_at,
                        updated_by
                    )
                    VALUES
                    (
                        @codigo_inspeccion,
                        @version,
                        @titulo,
                        @resumen,
                        @resultado,
                        @observaciones,
                        @conclusiones,
                        @recomendaciones,
                        FALSE,
                        FALSE,
                        NOW(),
                        @created_by,
                        NOW(),
                        @updated_by
                    )
                    RETURNING codigo_informe;";

                using (var cmd = new NpgsqlCommand(insertSql, cn))
                {
                    Bind(cmd, informe, usuarioId);
                    cmd.Parameters.AddWithValue("@version", version);
                    cmd.Parameters.AddWithValue("@created_by", usuarioId);
                    var codigoInforme = Convert.ToInt32(cmd.ExecuteScalar());
                    return ObtenerPorIdInterno(cn, codigoInforme);
                }
            }
        }

        public void MarcarFinalizado(int codigoInforme, string rutaPdf, bool correoEnviado, string estadoInforme, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tbinforme_inspeccion
                    SET ruta_pdf = @ruta_pdf,
                        estado_informe = @estado_informe,
                        finalizado = TRUE,
                        correo_enviado = @correo_enviado,
                        fecha_finalizacion = NOW(),
                        updated_at = NOW(),
                        updated_by = @updated_by
                    WHERE codigo_informe = @codigo_informe;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_informe", codigoInforme);
                    cmd.Parameters.AddWithValue("@ruta_pdf", (object)rutaPdf ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado_informe", (object)estadoInforme ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@correo_enviado", correoEnviado);
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public InspeccionInformeTecnico ObtenerPorId(int codigoInforme)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);
                return ObtenerPorIdInterno(cn, codigoInforme);
            }
        }

        public void RegistrarFirmaInspector(int codigoInforme, string rutaDocumentoFirmado, string hashDocumento, DateTime fechaFirma, string usuarioFirma, string estadoInforme, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tbinforme_inspeccion
                    SET firmado_inspector = TRUE,
                        ruta_documento_firmado = @ruta_documento_firmado,
                        hash_documento = @hash_documento,
                        fecha_firma_1 = @fecha_firma_1,
                        usuario_firma_1 = @usuario_firma_1,
                        estado_informe = @estado_informe,
                        updated_at = NOW(),
                        updated_by = @updated_by
                    WHERE codigo_informe = @codigo_informe;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_informe", codigoInforme);
                    cmd.Parameters.AddWithValue("@ruta_documento_firmado", (object)rutaDocumentoFirmado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hash_documento", (object)hashDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma_1", fechaFirma);
                    cmd.Parameters.AddWithValue("@usuario_firma_1", (object)usuarioFirma ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado_informe", (object)estadoInforme ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void MarcarEnviadoADirdac(int codigoInforme, DateTime fechaEnvio, string usuarioEnvio, bool correoEnviado, string estadoInforme, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tbinforme_inspeccion
                    SET correo_enviado = @correo_enviado,
                        fecha_envio_dirdac = @fecha_envio_dirdac,
                        usuario_envio_dirdac = @usuario_envio_dirdac,
                        estado_informe = @estado_informe,
                        updated_at = NOW(),
                        updated_by = @updated_by
                    WHERE codigo_informe = @codigo_informe;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_informe", codigoInforme);
                    cmd.Parameters.AddWithValue("@correo_enviado", correoEnviado);
                    cmd.Parameters.AddWithValue("@fecha_envio_dirdac", fechaEnvio);
                    cmd.Parameters.AddWithValue("@usuario_envio_dirdac", (object)usuarioEnvio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado_informe", (object)estadoInforme ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RegistrarFirmaDirdac(int codigoInforme, string rutaDocumentoFirmado, string hashDocumento, DateTime fechaFirma, string usuarioFirma, string estadoInforme, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tbinforme_inspeccion
                    SET firmado_dirdac = TRUE,
                        ruta_documento_firmado = @ruta_documento_firmado,
                        hash_documento = @hash_documento,
                        fecha_firma_2 = @fecha_firma_2,
                        usuario_firma_2 = @usuario_firma_2,
                        estado_informe = @estado_informe,
                        updated_at = NOW(),
                        updated_by = @updated_by
                    WHERE codigo_informe = @codigo_informe;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_informe", codigoInforme);
                    cmd.Parameters.AddWithValue("@ruta_documento_firmado", (object)rutaDocumentoFirmado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hash_documento", (object)hashDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma_2", fechaFirma);
                    cmd.Parameters.AddWithValue("@usuario_firma_2", (object)usuarioFirma ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado_informe", (object)estadoInforme ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void ActualizarCorreoEnviado(int codigoInforme, bool correoEnviado, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tbinforme_inspeccion
                    SET correo_enviado = @correo_enviado,
                        updated_at = NOW(),
                        updated_by = @updated_by
                    WHERE codigo_informe = @codigo_informe;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_informe", codigoInforme);
                    cmd.Parameters.AddWithValue("@correo_enviado", correoEnviado);
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void Bind(NpgsqlCommand cmd, InspeccionInformeTecnico informe, int usuarioId)
        {
            cmd.Parameters.AddWithValue("@codigo_inspeccion", informe.CodigoInspeccion);
            cmd.Parameters.AddWithValue("@titulo", (object)informe.Titulo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@resumen", (object)informe.Resumen ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@resultado", (object)informe.Resultado ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@observaciones", (object)informe.Observaciones ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@conclusiones", (object)informe.Conclusiones ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@recomendaciones", (object)informe.Recomendaciones ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@updated_by", usuarioId);
        }

        private static InspeccionInformeTecnico Map(NpgsqlDataReader dr)
        {
            return new InspeccionInformeTecnico
            {
                CodigoInforme = dr.GetInt32(dr.GetOrdinal("codigo_informe")),
                CodigoInspeccion = dr.GetInt32(dr.GetOrdinal("codigo_inspeccion")),
                Version = dr.GetInt32(dr.GetOrdinal("version")),
                Titulo = dr["titulo"] == DBNull.Value ? null : dr["titulo"].ToString(),
                Resumen = dr["resumen"] == DBNull.Value ? null : dr["resumen"].ToString(),
                Resultado = dr["resultado"] == DBNull.Value ? null : dr["resultado"].ToString(),
                Observaciones = dr["observaciones"] == DBNull.Value ? null : dr["observaciones"].ToString(),
                Conclusiones = dr["conclusiones"] == DBNull.Value ? null : dr["conclusiones"].ToString(),
                Recomendaciones = dr["recomendaciones"] == DBNull.Value ? null : dr["recomendaciones"].ToString(),
                RutaPdf = dr["ruta_pdf"] == DBNull.Value ? null : dr["ruta_pdf"].ToString(),
                EstadoInforme = dr["estado_informe"] == DBNull.Value ? null : dr["estado_informe"].ToString(),
                FirmadoInspector = dr["firmado_inspector"] != DBNull.Value && Convert.ToBoolean(dr["firmado_inspector"]),
                FirmadoDirdac = dr["firmado_dirdac"] != DBNull.Value && Convert.ToBoolean(dr["firmado_dirdac"]),
                RutaDocumentoFirmado = dr["ruta_documento_firmado"] == DBNull.Value ? null : dr["ruta_documento_firmado"].ToString(),
                HashDocumento = dr["hash_documento"] == DBNull.Value ? null : dr["hash_documento"].ToString(),
                FechaFirma1 = dr["fecha_firma_1"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(dr["fecha_firma_1"]),
                FechaFirma2 = dr["fecha_firma_2"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(dr["fecha_firma_2"]),
                UsuarioFirma1 = dr["usuario_firma_1"] == DBNull.Value ? null : dr["usuario_firma_1"].ToString(),
                UsuarioFirma2 = dr["usuario_firma_2"] == DBNull.Value ? null : dr["usuario_firma_2"].ToString(),
                FechaEnvioDirdac = dr["fecha_envio_dirdac"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(dr["fecha_envio_dirdac"]),
                UsuarioEnvioDirdac = dr["usuario_envio_dirdac"] == DBNull.Value ? null : dr["usuario_envio_dirdac"].ToString(),
                Finalizado = dr["finalizado"] != DBNull.Value && Convert.ToBoolean(dr["finalizado"]),
                CorreoEnviado = dr["correo_enviado"] != DBNull.Value && Convert.ToBoolean(dr["correo_enviado"]),
                FechaFinalizacion = dr["fecha_finalizacion"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(dr["fecha_finalizacion"]),
                CreatedAt = dr["created_at"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(dr["created_at"]),
                CreatedBy = dr["created_by"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["created_by"]),
                UpdatedAt = dr["updated_at"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(dr["updated_at"]),
                UpdatedBy = dr["updated_by"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["updated_by"])
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
                    CREATE TABLE IF NOT EXISTS public.aocr_tbinforme_inspeccion
                    (
                        codigo_informe SERIAL PRIMARY KEY,
                        codigo_inspeccion INTEGER NOT NULL,
                        version INTEGER NOT NULL,
                        titulo VARCHAR(250),
                        resumen TEXT,
                        resultado VARCHAR(100),
                        observaciones TEXT,
                        conclusiones TEXT,
                        recomendaciones TEXT,
                        ruta_pdf VARCHAR(500),
                        estado_informe VARCHAR(80),
                        firmado_inspector BOOLEAN NOT NULL DEFAULT FALSE,
                        firmado_dirdac BOOLEAN NOT NULL DEFAULT FALSE,
                        ruta_documento_firmado VARCHAR(500),
                        hash_documento VARCHAR(256),
                        fecha_firma_1 TIMESTAMP,
                        fecha_firma_2 TIMESTAMP,
                        usuario_firma_1 VARCHAR(160),
                        usuario_firma_2 VARCHAR(160),
                        fecha_envio_dirdac TIMESTAMP,
                        usuario_envio_dirdac VARCHAR(160),
                        finalizado BOOLEAN NOT NULL DEFAULT FALSE,
                        correo_enviado BOOLEAN NOT NULL DEFAULT FALSE,
                        fecha_finalizacion TIMESTAMP,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        created_by INTEGER,
                        updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        updated_by INTEGER
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS uq_informe_inspeccion_version
                        ON public.aocr_tbinforme_inspeccion(codigo_inspeccion, version);

                    CREATE INDEX IF NOT EXISTS idx_informe_inspeccion_codigo
                        ON public.aocr_tbinforme_inspeccion(codigo_inspeccion, version DESC);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                const string alterSql = @"
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS estado_informe VARCHAR(80);
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS firmado_inspector BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS firmado_dirdac BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS ruta_documento_firmado VARCHAR(500);
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS hash_documento VARCHAR(256);
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS fecha_firma_1 TIMESTAMP;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS fecha_firma_2 TIMESTAMP;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS usuario_firma_1 VARCHAR(160);
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS usuario_firma_2 VARCHAR(160);
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS fecha_envio_dirdac TIMESTAMP;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS usuario_envio_dirdac VARCHAR(160);";

                using (var cmd = new NpgsqlCommand(alterSql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }

        private static InspeccionInformeTecnico ObtenerUltimoPorInspeccionInterno(NpgsqlConnection cn, int codigoInspeccion)
        {
            const string sql = @"
                SELECT codigo_informe,
                       codigo_inspeccion,
                       version,
                       titulo,
                       resumen,
                       resultado,
                       observaciones,
                       conclusiones,
                       recomendaciones,
                       ruta_pdf,
                      estado_informe,
                      firmado_inspector,
                      firmado_dirdac,
                      ruta_documento_firmado,
                      hash_documento,
                      fecha_firma_1,
                      fecha_firma_2,
                      usuario_firma_1,
                      usuario_firma_2,
                      fecha_envio_dirdac,
                      usuario_envio_dirdac,
                       finalizado,
                       correo_enviado,
                       fecha_finalizacion,
                       created_at,
                       created_by,
                       updated_at,
                       updated_by
                FROM public.aocr_tbinforme_inspeccion
                WHERE codigo_inspeccion = @codigo_inspeccion
                ORDER BY version DESC
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_inspeccion", codigoInspeccion);
                using (var dr = cmd.ExecuteReader())
                {
                    return dr.Read() ? Map(dr) : null;
                }
            }
        }

        private static InspeccionInformeTecnico ObtenerPorIdInterno(NpgsqlConnection cn, int codigoInforme)
        {
            const string sql = @"
                SELECT codigo_informe,
                       codigo_inspeccion,
                       version,
                       titulo,
                       resumen,
                       resultado,
                       observaciones,
                       conclusiones,
                       recomendaciones,
                       ruta_pdf,
                      estado_informe,
                      firmado_inspector,
                      firmado_dirdac,
                      ruta_documento_firmado,
                      hash_documento,
                      fecha_firma_1,
                      fecha_firma_2,
                      usuario_firma_1,
                      usuario_firma_2,
                      fecha_envio_dirdac,
                      usuario_envio_dirdac,
                       finalizado,
                       correo_enviado,
                       fecha_finalizacion,
                       created_at,
                       created_by,
                       updated_at,
                       updated_by
                FROM public.aocr_tbinforme_inspeccion
                WHERE codigo_informe = @codigo_informe
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_informe", codigoInforme);
                using (var dr = cmd.ExecuteReader())
                {
                    return dr.Read() ? Map(dr) : null;
                }
            }
        }
    }
}