using System;
using System.Collections.Generic;
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
                           antecedentes,
                           alcance,
                           desarrollo,
                           evidencias,
                           numero_licencia_inspector,
                           trabajos_realizados,
                           fechas_inspeccion_manual,
                           estaciones_inspeccion_manual,
                           operacion_comercial,
                           servicios_estaciones,
                           notas,
                           no_conformidades,
                           documentos_adjuntos,
                           documentos_adjuntos_archivos,
                           otros_adjuntos,
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

        public List<InspeccionInformeTecnico> ListarPendientesFirmaDirdac()
        {
            var lista = new List<InspeccionInformeTecnico>();

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT DISTINCT ON (codigo_inspeccion)
                           codigo_informe,
                           codigo_inspeccion,
                           version,
                           titulo,
                           resumen,
                           antecedentes,
                           alcance,
                           desarrollo,
                           evidencias,
                           numero_licencia_inspector,
                           trabajos_realizados,
                           fechas_inspeccion_manual,
                           estaciones_inspeccion_manual,
                           operacion_comercial,
                           servicios_estaciones,
                           notas,
                           no_conformidades,
                           documentos_adjuntos,
                           documentos_adjuntos_archivos,
                           otros_adjuntos,
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
                    WHERE finalizado = TRUE
                      AND COALESCE(firmado_dirdac, FALSE) = FALSE
                       AND regexp_replace(UPPER(COALESCE(estado_informe, '')), '[\s_-]+', '_', 'g') = 'ENVIADO_A_DIRDAC'
                    ORDER BY codigo_inspeccion, version DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        lista.Add(Map(dr));
                    }
                }
            }

            return lista;
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
                            antecedentes = @antecedentes,
                            alcance = @alcance,
                            desarrollo = @desarrollo,
                            evidencias = @evidencias,
                            numero_licencia_inspector = @numero_licencia_inspector,
                            trabajos_realizados = @trabajos_realizados,
                            fechas_inspeccion_manual = @fechas_inspeccion_manual,
                            estaciones_inspeccion_manual = @estaciones_inspeccion_manual,
                            operacion_comercial = @operacion_comercial,
                            servicios_estaciones = @servicios_estaciones,
                            notas = @notas,
                            no_conformidades = @no_conformidades,
                            documentos_adjuntos = @documentos_adjuntos,
                            documentos_adjuntos_archivos = @documentos_adjuntos_archivos,
                            otros_adjuntos = @otros_adjuntos,
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
                        antecedentes,
                        alcance,
                        desarrollo,
                        evidencias,
                        numero_licencia_inspector,
                        trabajos_realizados,
                        fechas_inspeccion_manual,
                        estaciones_inspeccion_manual,
                        operacion_comercial,
                        servicios_estaciones,
                        notas,
                        no_conformidades,
                        documentos_adjuntos,
                        documentos_adjuntos_archivos,
                        otros_adjuntos,
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
                        @antecedentes,
                        @alcance,
                        @desarrollo,
                        @evidencias,
                        @numero_licencia_inspector,
                        @trabajos_realizados,
                        @fechas_inspeccion_manual,
                        @estaciones_inspeccion_manual,
                        @operacion_comercial,
                        @servicios_estaciones,
                        @notas,
                        @no_conformidades,
                        @documentos_adjuntos,
                        @documentos_adjuntos_archivos,
                        @otros_adjuntos,
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

        public void RegistrarAprobacionDireccion(int codigoInforme, DateTime fechaRevision, string usuarioRevision, string estadoInforme, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tbinforme_inspeccion
                    SET fecha_firma_2 = @fecha_firma_2,
                        usuario_firma_2 = @usuario_firma_2,
                        estado_informe = @estado_informe,
                        updated_at = NOW(),
                        updated_by = @updated_by
                    WHERE codigo_informe = @codigo_informe;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_informe", codigoInforme);
                    cmd.Parameters.AddWithValue("@fecha_firma_2", fechaRevision);
                    cmd.Parameters.AddWithValue("@usuario_firma_2", (object)usuarioRevision ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado_informe", (object)estadoInforme ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void ActualizarEstadoInforme(int codigoInforme, string estadoInforme, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tbinforme_inspeccion
                    SET estado_informe = @estado_informe,
                        updated_at = NOW(),
                        updated_by = @updated_by
                    WHERE codigo_informe = @codigo_informe;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_informe", codigoInforme);
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

        public void RegistrarDevolucionCoordinador(int codigoInforme, string observacion, string usuarioDevolucion, string estadoInforme, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tbinforme_inspeccion
                    SET observacion_devolucion = @observacion_devolucion,
                        fecha_devolucion = NOW(),
                        usuario_devolucion = @usuario_devolucion,
                        estado_informe = @estado_informe,
                        updated_at = NOW(),
                        updated_by = @updated_by
                    WHERE codigo_informe = @codigo_informe;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_informe", codigoInforme);
                    cmd.Parameters.AddWithValue("@observacion_devolucion", (object)observacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_devolucion", (object)usuarioDevolucion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado_informe", (object)estadoInforme ?? DBNull.Value);
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
            cmd.Parameters.AddWithValue("@antecedentes", (object)informe.Antecedentes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@alcance", (object)informe.Alcance ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@desarrollo", (object)informe.Desarrollo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@evidencias", (object)informe.Evidencias ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@numero_licencia_inspector", (object)informe.NumeroLicenciaInspector ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@trabajos_realizados", (object)informe.TrabajosRealizados ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fechas_inspeccion_manual", (object)informe.FechasInspeccionManual ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@estaciones_inspeccion_manual", (object)informe.EstacionesInspeccionManual ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@operacion_comercial", (object)informe.OperacionComercial ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@servicios_estaciones", (object)informe.ServiciosEstaciones ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notas", (object)informe.Notas ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@no_conformidades", (object)informe.NoConformidades ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@documentos_adjuntos", (object)informe.DocumentosAdjuntos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@documentos_adjuntos_archivos", (object)informe.DocumentosAdjuntosArchivos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@otros_adjuntos", (object)informe.OtrosAdjuntos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@resultado", (object)informe.Resultado ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@observaciones", (object)informe.Observaciones ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@conclusiones", (object)informe.Conclusiones ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@recomendaciones", (object)informe.Recomendaciones ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@updated_by", usuarioId);
        }

        private static InspeccionInformeTecnico Map(NpgsqlDataReader dr)
        {
            var m = new InspeccionInformeTecnico
            {
                CodigoInforme = dr.GetInt32(dr.GetOrdinal("codigo_informe")),
                CodigoInspeccion = dr.GetInt32(dr.GetOrdinal("codigo_inspeccion")),
                Version = dr.GetInt32(dr.GetOrdinal("version")),
                Titulo = dr["titulo"] == DBNull.Value ? null : dr["titulo"].ToString(),
                Resumen = dr["resumen"] == DBNull.Value ? null : dr["resumen"].ToString(),
                Antecedentes = dr["antecedentes"] == DBNull.Value ? null : dr["antecedentes"].ToString(),
                Alcance = dr["alcance"] == DBNull.Value ? null : dr["alcance"].ToString(),
                Desarrollo = dr["desarrollo"] == DBNull.Value ? null : dr["desarrollo"].ToString(),
                Evidencias = dr["evidencias"] == DBNull.Value ? null : dr["evidencias"].ToString(),
                NumeroLicenciaInspector = dr["numero_licencia_inspector"] == DBNull.Value ? null : dr["numero_licencia_inspector"].ToString(),
                TrabajosRealizados = dr["trabajos_realizados"] == DBNull.Value ? null : dr["trabajos_realizados"].ToString(),
                FechasInspeccionManual = dr["fechas_inspeccion_manual"] == DBNull.Value ? null : dr["fechas_inspeccion_manual"].ToString(),
                EstacionesInspeccionManual = dr["estaciones_inspeccion_manual"] == DBNull.Value ? null : dr["estaciones_inspeccion_manual"].ToString(),
                OperacionComercial = dr["operacion_comercial"] == DBNull.Value ? null : dr["operacion_comercial"].ToString(),
                ServiciosEstaciones = dr["servicios_estaciones"] == DBNull.Value ? null : dr["servicios_estaciones"].ToString(),
                Notas = dr["notas"] == DBNull.Value ? null : dr["notas"].ToString(),
                NoConformidades = dr["no_conformidades"] == DBNull.Value ? null : dr["no_conformidades"].ToString(),
                DocumentosAdjuntos = dr["documentos_adjuntos"] == DBNull.Value ? null : dr["documentos_adjuntos"].ToString(),
                DocumentosAdjuntosArchivos = dr["documentos_adjuntos_archivos"] == DBNull.Value ? null : dr["documentos_adjuntos_archivos"].ToString(),
                OtrosAdjuntos = dr["otros_adjuntos"] == DBNull.Value ? null : dr["otros_adjuntos"].ToString(),
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

            try { m.ObservacionDevolucion = dr["observacion_devolucion"] == DBNull.Value ? null : dr["observacion_devolucion"].ToString(); } catch { }
            try { m.FechaDevolucion = dr["fecha_devolucion"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(dr["fecha_devolucion"]); } catch { }
            try { m.UsuarioDevolucion = dr["usuario_devolucion"] == DBNull.Value ? null : dr["usuario_devolucion"].ToString(); } catch { }

            return m;
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
                        antecedentes TEXT,
                        alcance TEXT,
                        desarrollo TEXT,
                        evidencias TEXT,
                        numero_licencia_inspector VARCHAR(120),
                        trabajos_realizados TEXT,
                        fechas_inspeccion_manual TEXT,
                        estaciones_inspeccion_manual TEXT,
                        operacion_comercial TEXT,
                        servicios_estaciones TEXT,
                        notas TEXT,
                        no_conformidades TEXT,
                        documentos_adjuntos TEXT,
                        documentos_adjuntos_archivos TEXT,
                        otros_adjuntos TEXT,
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
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS antecedentes TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS alcance TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS desarrollo TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS evidencias TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS numero_licencia_inspector VARCHAR(120);
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS trabajos_realizados TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS fechas_inspeccion_manual TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS estaciones_inspeccion_manual TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS operacion_comercial TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS servicios_estaciones TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS notas TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS no_conformidades TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS documentos_adjuntos TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS documentos_adjuntos_archivos TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS otros_adjuntos TEXT;
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
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS usuario_envio_dirdac VARCHAR(160);
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS observacion_devolucion TEXT;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS fecha_devolucion TIMESTAMP;
                    ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS usuario_devolucion VARCHAR(160);";

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
                       antecedentes,
                       alcance,
                       desarrollo,
                       evidencias,
                       numero_licencia_inspector,
                       trabajos_realizados,
                       fechas_inspeccion_manual,
                       estaciones_inspeccion_manual,
                       operacion_comercial,
                       servicios_estaciones,
                       notas,
                       no_conformidades,
                       documentos_adjuntos,
                       documentos_adjuntos_archivos,
                       otros_adjuntos,
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
                       antecedentes,
                       alcance,
                       desarrollo,
                       evidencias,
                       numero_licencia_inspector,
                       trabajos_realizados,
                       fechas_inspeccion_manual,
                       estaciones_inspeccion_manual,
                       operacion_comercial,
                       servicios_estaciones,
                       notas,
                       no_conformidades,
                       documentos_adjuntos,
                       documentos_adjuntos_archivos,
                       otros_adjuntos,
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
