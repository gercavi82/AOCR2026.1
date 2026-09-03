using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using Npgsql;
using CapaDatos.Constants;
using CapaModelo;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// AC-07: DAO para Lista de Verificación (LV) por inspección o estación solicitada.
    /// Garantiza independencia relacional por estación, versionado histórico, inmutabilidad tras firma y transaccionalidad atómica.
    /// </summary>
    public class ListaVerificacionOperacionalEaeDAO
    {
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;
        private readonly string _cs;

        public ListaVerificacionOperacionalEaeDAO()
        {
            _cs = !string.IsNullOrWhiteSpace(ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString)
                ? ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public ListaVerificacionOperacionalEae ObtenerUltimaPorInspeccion(int codigoInspeccion, int? estacionId = null)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);
                return ObtenerUltimaPorInspeccionInterno(cn, codigoInspeccion, estacionId);
            }
        }

        public ListaVerificacionOperacionalEae ObtenerPorInspeccionYEstacion(int codigoInspeccion, int? estacionId)
        {
            return ObtenerUltimaPorInspeccion(codigoInspeccion, estacionId);
        }

        public ListaVerificacionOperacionalEae ObtenerPorId(int codigoListaVerificacion)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);
                return ObtenerPorIdInterno(cn, codigoListaVerificacion);
            }
        }

        public IList<ListaVerificacionOperacionalEae> ListarPorSolicitud(int solicitudId)
        {
            var resultados = new List<ListaVerificacionOperacionalEae>();
            if (solicitudId <= 0) return resultados;

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT lv.*, 
                           est.estacion_codigo AS estacion_codigo,
                           est.estacion_nombre AS estacion_nombre
                      FROM public.aocr_tblv_operacional_eae lv
                 LEFT JOIN public.aocr_tbsolicitud_estacion est ON est.id = lv.estacion_id
                     WHERE (lv.solicitud_id = @solicitud_id 
                            OR lv.codigo_inspeccion IN (SELECT codigo_inspeccion FROM public.aocr_tbinspeccion WHERE codigo_solicitud = @solicitud_id))
                       AND lv.vigente = TRUE
                  ORDER BY COALESCE(lv.estacion_id, 0) ASC, lv.version DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            resultados.Add(Map(dr));
                        }
                    }
                }
            }

            return resultados;
        }

        public IList<ListaVerificacionOperacionalEae> ListarConPdfHistorico()
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT codigo_lv,
                           codigo_inspeccion,
                           solicitud_id,
                           estacion_id,
                           tipo_lista,
                           vigente,
                           version,
                           estado_lista,
                           nombre_eae,
                           numero_aoc_fecha_validez,
                           direccion_estado_explotador,
                           direccion_estado_reconocimiento,
                           tipos_aeronaves,
                           tipo_operacion,
                           fecha_lista,
                           inspector_responsable,
                           cargo_inspector,
                           resumen_verificacion,
                           observaciones_generales,
                           resultado_general,
                           items_json,
                           ruta_pdf,
                           ruta_documento_firmado,
                           hash_documento,
                           finalizado,
                           firmado_tecnico,
                           fecha_finalizacion,
                           fecha_firma,
                           usuario_firma,
                           created_at,
                           created_by,
                           updated_at,
                           updated_by
                      FROM public.aocr_tblv_operacional_eae
                     WHERE ruta_pdf IS NOT NULL
                       AND LENGTH(TRIM(ruta_pdf)) > 0
                  ORDER BY codigo_lv DESC;";

                var resultados = new List<ListaVerificacionOperacionalEae>();
                using (var cmd = new NpgsqlCommand(sql, cn))
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        resultados.Add(Map(dr));
                    }
                }

                return resultados;
            }
        }

        public ListaVerificacionOperacionalEae GuardarBorrador(ListaVerificacionOperacionalEae lista, int usuarioId)
        {
            if (lista == null)
            {
                throw new ArgumentNullException(nameof(lista));
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                var ultima = ObtenerUltimaPorInspeccionInterno(cn, lista.CodigoInspeccion, lista.EstacionId);
                if (ultima != null && !ultima.Finalizado && !ultima.FirmadoTecnico)
                {
                    const string updateSql = @"
                        UPDATE public.aocr_tblv_operacional_eae
                           SET estado_lista = @estado_lista,
                               nombre_eae = @nombre_eae,
                               numero_aoc_fecha_validez = @numero_aoc_fecha_validez,
                               direccion_estado_explotador = @direccion_estado_explotador,
                               direccion_estado_reconocimiento = @direccion_estado_reconocimiento,
                               tipos_aeronaves = @tipos_aeronaves,
                               tipo_operacion = @tipo_operacion,
                               fecha_lista = @fecha_lista,
                               inspector_responsable = @inspector_responsable,
                               cargo_inspector = @cargo_inspector,
                               resumen_verificacion = @resumen_verificacion,
                               observaciones_generales = @observaciones_generales,
                               resultado_general = @resultado_general,
                               items_json = @items_json,
                               solicitud_id = COALESCE(@solicitud_id, solicitud_id),
                               estacion_id = @estacion_id,
                               tipo_lista = @tipo_lista,
                               vigente = TRUE,
                               updated_at = NOW(),
                               updated_by = @updated_by
                         WHERE codigo_lv = @codigo_lv;";

                    using (var cmd = new NpgsqlCommand(updateSql, cn))
                    {
                        Bind(cmd, lista, usuarioId);
                        cmd.Parameters.AddWithValue("@codigo_lv", ultima.CodigoListaVerificacion);
                        cmd.ExecuteNonQuery();
                    }

                    return ObtenerPorIdInterno(cn, ultima.CodigoListaVerificacion);
                }

                // Inactivar vigencia previa si existe
                if (ultima != null)
                {
                    const string inactivaSql = @"
                        UPDATE public.aocr_tblv_operacional_eae
                           SET vigente = FALSE,
                               updated_at = NOW(),
                               updated_by = @updated_by
                         WHERE codigo_lv = @codigo_lv;";
                    using (var cmdInac = new NpgsqlCommand(inactivaSql, cn))
                    {
                        cmdInac.Parameters.AddWithValue("@codigo_lv", ultima.CodigoListaVerificacion);
                        cmdInac.Parameters.AddWithValue("@updated_by", usuarioId);
                        cmdInac.ExecuteNonQuery();
                    }
                }

                var version = ultima != null ? ultima.Version + 1 : 1;
                const string insertSql = @"
                    INSERT INTO public.aocr_tblv_operacional_eae
                    (
                        codigo_inspeccion,
                        solicitud_id,
                        estacion_id,
                        tipo_lista,
                        vigente,
                        version,
                        estado_lista,
                        nombre_eae,
                        numero_aoc_fecha_validez,
                        direccion_estado_explotador,
                        direccion_estado_reconocimiento,
                        tipos_aeronaves,
                        tipo_operacion,
                        fecha_lista,
                        inspector_responsable,
                        cargo_inspector,
                        resumen_verificacion,
                        observaciones_generales,
                        resultado_general,
                        items_json,
                        finalizado,
                        firmado_tecnico,
                        created_at,
                        created_by,
                        updated_at,
                        updated_by
                    )
                    VALUES
                    (
                        @codigo_inspeccion,
                        @solicitud_id,
                        @estacion_id,
                        @tipo_lista,
                        TRUE,
                        @version,
                        @estado_lista,
                        @nombre_eae,
                        @numero_aoc_fecha_validez,
                        @direccion_estado_explotador,
                        @direccion_estado_reconocimiento,
                        @tipos_aeronaves,
                        @tipo_operacion,
                        @fecha_lista,
                        @inspector_responsable,
                        @cargo_inspector,
                        @resumen_verificacion,
                        @observaciones_generales,
                        @resultado_general,
                        @items_json,
                        FALSE,
                        FALSE,
                        NOW(),
                        @created_by,
                        NOW(),
                        @updated_by
                    )
                    RETURNING codigo_lv;";

                using (var cmd = new NpgsqlCommand(insertSql, cn))
                {
                    Bind(cmd, lista, usuarioId);
                    cmd.Parameters.AddWithValue("@version", version);
                    var codigo = Convert.ToInt32(cmd.ExecuteScalar());
                    return ObtenerPorIdInterno(cn, codigo);
                }
            }
        }

        public void MarcarFinalizada(int codigoListaVerificacion, string rutaPdf, string estadoLista, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tblv_operacional_eae
                       SET finalizado = TRUE,
                           fecha_finalizacion = NOW(),
                           estado_lista = @estado_lista,
                           ruta_pdf = CASE WHEN @ruta_pdf IS NOT NULL AND LENGTH(TRIM(@ruta_pdf)) > 0 THEN @ruta_pdf ELSE ruta_pdf END,
                           updated_at = NOW(),
                           updated_by = @updated_by
                     WHERE codigo_lv = @codigo_lv;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@estado_lista", string.IsNullOrWhiteSpace(estadoLista) ? AocrEstadosListaVerificacion.Completa : estadoLista);
                    cmd.Parameters.AddWithValue("@ruta_pdf", (object)(rutaPdf ?? string.Empty));
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.Parameters.AddWithValue("@codigo_lv", codigoListaVerificacion);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RegistrarFirmaTecnico(
            int codigoListaVerificacion,
            string rutaDocumentoFirmado,
            string hashDocumento,
            DateTime fechaFirma,
            string usuarioFirma,
            string estadoLista,
            int usuarioId)
        {
            MarcarFirmada(codigoListaVerificacion, rutaDocumentoFirmado, hashDocumento, usuarioFirma, fechaFirma, estadoLista, usuarioId);
        }

        public void MarcarFirmada(
            int codigoListaVerificacion,
            string rutaDocumentoFirmado,
            string hashDocumento,
            string usuarioFirma,
            DateTime fechaFirma,
            string estadoLista,
            int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tblv_operacional_eae
                       SET firmado_tecnico = TRUE,
                           finalizado = TRUE,
                           fecha_firma = @fecha_firma,
                           usuario_firma = @usuario_firma,
                           hash_documento = @hash_documento,
                           estado_lista = @estado_lista,
                           ruta_documento_firmado = @ruta_documento_firmado,
                           updated_at = NOW(),
                           updated_by = @updated_by
                     WHERE codigo_lv = @codigo_lv;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@fecha_firma", fechaFirma);
                    cmd.Parameters.AddWithValue("@usuario_firma", (object)(usuarioFirma ?? string.Empty));
                    cmd.Parameters.AddWithValue("@hash_documento", (object)(hashDocumento ?? string.Empty));
                    cmd.Parameters.AddWithValue("@estado_lista", string.IsNullOrWhiteSpace(estadoLista) ? AocrEstadosListaVerificacion.Firmada : estadoLista);
                    cmd.Parameters.AddWithValue("@ruta_documento_firmado", (object)(rutaDocumentoFirmado ?? string.Empty));
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.Parameters.AddWithValue("@codigo_lv", codigoListaVerificacion);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void ActualizarRutaPdf(int codigoListaVerificacion, string rutaPdf, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tblv_operacional_eae
                       SET ruta_pdf = @ruta_pdf,
                           updated_at = NOW(),
                           updated_by = @updated_by
                     WHERE codigo_lv = @codigo_lv;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@ruta_pdf", (object)(rutaPdf ?? string.Empty));
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.Parameters.AddWithValue("@codigo_lv", codigoListaVerificacion);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Comprueba si todas las estaciones obligatorias de una solicitud cuentan con su LV completada y firmada.
        /// </summary>
        public bool TodasLasListasEstacionesFirmadas(int solicitudId, int inspeccionId, out List<string> estacionesPendientes)
        {
            estacionesPendientes = new List<string>();
            if (solicitudId <= 0 && inspeccionId <= 0) return true;

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                // 1. Obtener las estaciones activas de la solicitud
                const string sqlEstaciones = @"
                    SELECT id, estacion_codigo, estacion_nombre
                      FROM public.aocr_tbsolicitud_estacion
                     WHERE solicitud_id = @solicitud_id
                       AND activo = TRUE
                  ORDER BY id ASC;";

                var listaEstaciones = new List<Tuple<int, string, string>>();
                using (var cmd = new NpgsqlCommand(sqlEstaciones, cn))
                {
                    cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            listaEstaciones.Add(Tuple.Create(
                                rd.GetInt32(0),
                                rd.IsDBNull(1) ? string.Empty : rd.GetString(1),
                                rd.IsDBNull(2) ? string.Empty : rd.GetString(2)
                            ));
                        }
                    }
                }

                // Si no hay estaciones registradas (caso mono-estación histórica), validar la LV general de la inspección
                if (listaEstaciones.Count == 0)
                {
                    var lvGeneral = ObtenerUltimaPorInspeccionInterno(cn, inspeccionId, null);
                    if (lvGeneral == null || !lvGeneral.FirmadoTecnico)
                    {
                        estacionesPendientes.Add("Inspección Principal");
                        return false;
                    }
                    return true;
                }

                // 2. Comprobar que cada estación tenga una LV vigente firmada
                foreach (var est in listaEstaciones)
                {
                    var lvEst = ObtenerUltimaPorInspeccionInterno(cn, inspeccionId, est.Item1);
                    if (lvEst == null || !lvEst.FirmadoTecnico)
                    {
                        var etiqueta = !string.IsNullOrWhiteSpace(est.Item2)
                            ? $"{est.Item2} ({est.Item3})"
                            : (string.IsNullOrWhiteSpace(est.Item3) ? $"Estación #{est.Item1}" : est.Item3);
                        estacionesPendientes.Add(etiqueta);
                    }
                }

                return estacionesPendientes.Count == 0;
            }
        }

        private ListaVerificacionOperacionalEae ObtenerUltimaPorInspeccionInterno(NpgsqlConnection cn, int codigoInspeccion, int? estacionId)
        {
            var sql = @"
                SELECT lv.*, 
                       est.estacion_codigo AS estacion_codigo,
                       est.estacion_nombre AS estacion_nombre
                  FROM public.aocr_tblv_operacional_eae lv
             LEFT JOIN public.aocr_tbsolicitud_estacion est ON est.id = lv.estacion_id
                 WHERE lv.codigo_inspeccion = @codigo_inspeccion ";

            if (estacionId.HasValue && estacionId.Value > 0)
            {
                sql += " AND lv.estacion_id = @estacion_id ";
            }

            sql += " ORDER BY lv.version DESC LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_inspeccion", codigoInspeccion);
                if (estacionId.HasValue && estacionId.Value > 0)
                {
                    cmd.Parameters.AddWithValue("@estacion_id", estacionId.Value);
                }

                using (var dr = cmd.ExecuteReader())
                {
                    return dr.Read() ? Map(dr) : null;
                }
            }
        }

        private ListaVerificacionOperacionalEae ObtenerPorIdInterno(NpgsqlConnection cn, int codigoListaVerificacion)
        {
            const string sql = @"
                SELECT lv.*, 
                       est.estacion_codigo AS estacion_codigo,
                       est.estacion_nombre AS estacion_nombre
                  FROM public.aocr_tblv_operacional_eae lv
             LEFT JOIN public.aocr_tbsolicitud_estacion est ON est.id = lv.estacion_id
                 WHERE lv.codigo_lv = @codigo_lv;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_lv", codigoListaVerificacion);
                using (var dr = cmd.ExecuteReader())
                {
                    return dr.Read() ? Map(dr) : null;
                }
            }
        }

        private static ListaVerificacionOperacionalEae Map(NpgsqlDataReader dr)
        {
            var m = new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = GetInt(dr, "codigo_lv"),
                CodigoInspeccion = GetInt(dr, "codigo_inspeccion"),
                SolicitudId = GetNullableInt(dr, "solicitud_id"),
                EstacionId = GetNullableInt(dr, "estacion_id"),
                EstacionCodigo = GetString(dr, "estacion_codigo"),
                EstacionNombre = GetString(dr, "estacion_nombre"),
                TipoLista = GetString(dr, "tipo_lista") ?? "EAE",
                Vigente = GetBool(dr, "vigente"),
                Version = GetInt(dr, "version"),
                EstadoLista = GetString(dr, "estado_lista"),
                NombreEae = GetString(dr, "nombre_eae"),
                NumeroAocFechaValidez = GetString(dr, "numero_aoc_fecha_validez"),
                DireccionEstadoExplotador = GetString(dr, "direccion_estado_explotador"),
                DireccionEstadoReconocimiento = GetString(dr, "direccion_estado_reconocimiento"),
                TiposAeronaves = GetString(dr, "tipos_aeronaves"),
                TipoOperacion = GetString(dr, "tipo_operacion"),
                FechaLista = GetDateTime(dr, "fecha_lista"),
                InspectorResponsable = GetString(dr, "inspector_responsable"),
                CargoInspector = GetString(dr, "cargo_inspector"),
                ResumenVerificacion = GetString(dr, "resumen_verificacion"),
                ObservacionesGenerales = GetString(dr, "observaciones_generales"),
                ResultadoGeneral = GetString(dr, "resultado_general"),
                ItemsJson = GetString(dr, "items_json"),
                RutaPdf = GetString(dr, "ruta_pdf"),
                RutaDocumentoFirmado = GetString(dr, "ruta_documento_firmado"),
                HashDocumento = GetString(dr, "hash_documento"),
                Finalizado = GetBool(dr, "finalizado"),
                FirmadoTecnico = GetBool(dr, "firmado_tecnico"),
                FechaFinalizacion = GetDateTime(dr, "fecha_finalizacion"),
                FechaFirma = GetDateTime(dr, "fecha_firma"),
                UsuarioFirma = GetString(dr, "usuario_firma"),
                CreatedAt = GetDateTime(dr, "created_at"),
                CreatedBy = GetNullableInt(dr, "created_by"),
                UpdatedAt = GetDateTime(dr, "updated_at"),
                UpdatedBy = GetNullableInt(dr, "updated_by")
            };

            try { m.CodigoListaAnterior = GetNullableInt(dr, "codigo_lista_anterior"); } catch { }
            try { m.CodigoNoConformidadOrigen = GetNullableInt(dr, "codigo_no_conformidad_origen"); } catch { }
            try { m.CicloEvaluacion = GetInt(dr, "ciclo_evaluacion"); } catch { }
            try { m.EsReevaluacion = GetBool(dr, "es_reevaluacion"); } catch { }

            return m;
        }

        private static void Bind(NpgsqlCommand cmd, ListaVerificacionOperacionalEae lista, int usuarioId)
        {
            cmd.Parameters.AddWithValue("@codigo_inspeccion", lista.CodigoInspeccion);
            cmd.Parameters.AddWithValue("@solicitud_id", (object)lista.SolicitudId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@estacion_id", (object)lista.EstacionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tipo_lista", (object)(lista.TipoLista ?? "EAE"));
            cmd.Parameters.AddWithValue("@estado_lista", (object)(lista.EstadoLista ?? AocrEstadosListaVerificacion.Borrador));
            cmd.Parameters.AddWithValue("@nombre_eae", (object)(lista.NombreEae ?? string.Empty));
            cmd.Parameters.AddWithValue("@numero_aoc_fecha_validez", (object)(lista.NumeroAocFechaValidez ?? string.Empty));
            cmd.Parameters.AddWithValue("@direccion_estado_explotador", (object)(lista.DireccionEstadoExplotador ?? string.Empty));
            cmd.Parameters.AddWithValue("@direccion_estado_reconocimiento", (object)(lista.DireccionEstadoReconocimiento ?? string.Empty));
            cmd.Parameters.AddWithValue("@tipos_aeronaves", (object)(lista.TiposAeronaves ?? string.Empty));
            cmd.Parameters.AddWithValue("@tipo_operacion", (object)(lista.TipoOperacion ?? string.Empty));
            cmd.Parameters.AddWithValue("@fecha_lista", (object)lista.FechaLista ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@inspector_responsable", (object)(lista.InspectorResponsable ?? string.Empty));
            cmd.Parameters.AddWithValue("@cargo_inspector", (object)(lista.CargoInspector ?? string.Empty));
            cmd.Parameters.AddWithValue("@resumen_verificacion", (object)(lista.ResumenVerificacion ?? string.Empty));
            cmd.Parameters.AddWithValue("@observaciones_generales", (object)(lista.ObservacionesGenerales ?? string.Empty));
            cmd.Parameters.AddWithValue("@resultado_general", (object)(lista.ResultadoGeneral ?? string.Empty));
            cmd.Parameters.AddWithValue("@items_json", (object)(lista.ItemsJson ?? "[]"));
            cmd.Parameters.AddWithValue("@created_by", usuarioId);
            cmd.Parameters.AddWithValue("@updated_by", usuarioId);
        }

        private static int GetInt(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal))
            {
                return 0;
            }

            return dr.IsDBNull(ordinal) ? 0 : Convert.ToInt32(dr.GetValue(ordinal));
        }

        private static int? GetNullableInt(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal))
            {
                return null;
            }

            return dr.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(dr.GetValue(ordinal));
        }

        private static string GetString(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal))
            {
                return string.Empty;
            }

            return dr.IsDBNull(ordinal) ? string.Empty : Convert.ToString(dr.GetValue(ordinal));
        }

        private static bool GetBool(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal))
            {
                return false;
            }

            return !dr.IsDBNull(ordinal) && Convert.ToBoolean(dr.GetValue(ordinal));
        }

        private static DateTime? GetDateTime(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal))
            {
                return null;
            }

            return dr.IsDBNull(ordinal) ? (DateTime?)null : Convert.ToDateTime(dr.GetValue(ordinal));
        }

        private static bool TryGetOrdinal(NpgsqlDataReader dr, string column, out int ordinal)
        {
            ordinal = -1;
            if (dr == null || string.IsNullOrWhiteSpace(column))
            {
                return false;
            }

            for (var index = 0; index < dr.FieldCount; index++)
            {
                if (string.Equals(dr.GetName(index), column, StringComparison.OrdinalIgnoreCase))
                {
                    ordinal = index;
                    return true;
                }
            }

            return false;
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
                    CREATE TABLE IF NOT EXISTS public.aocr_tblv_operacional_eae
                    (
                        codigo_lv SERIAL PRIMARY KEY,
                        codigo_inspeccion INTEGER NOT NULL,
                        solicitud_id INTEGER NULL,
                        estacion_id INTEGER NULL,
                        tipo_lista VARCHAR(50) NOT NULL DEFAULT 'EAE',
                        vigente BOOLEAN NOT NULL DEFAULT TRUE,
                        version INTEGER NOT NULL DEFAULT 1,
                        estado_lista VARCHAR(50) NOT NULL DEFAULT 'LV_BORRADOR',
                        nombre_eae TEXT NULL,
                        numero_aoc_fecha_validez TEXT NULL,
                        direccion_estado_explotador TEXT NULL,
                        direccion_estado_reconocimiento TEXT NULL,
                        tipos_aeronaves TEXT NULL,
                        tipo_operacion TEXT NULL,
                        fecha_lista TIMESTAMP NULL,
                        inspector_responsable TEXT NULL,
                        cargo_inspector TEXT NULL,
                        resumen_verificacion TEXT NULL,
                        observaciones_generales TEXT NULL,
                        resultado_general VARCHAR(120) NULL,
                        items_json TEXT NULL,
                        ruta_pdf TEXT NULL,
                        ruta_documento_firmado TEXT NULL,
                        hash_documento VARCHAR(256) NULL,
                        finalizado BOOLEAN NOT NULL DEFAULT FALSE,
                        firmado_tecnico BOOLEAN NOT NULL DEFAULT FALSE,
                        fecha_finalizacion TIMESTAMP NULL,
                        fecha_firma TIMESTAMP NULL,
                        usuario_firma VARCHAR(250) NULL,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        created_by INTEGER NULL,
                        updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        updated_by INTEGER NULL
                    );

                    -- Columnas aditivas idempotentes
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS solicitud_id INTEGER NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS estacion_id INTEGER NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS tipo_lista VARCHAR(50) NOT NULL DEFAULT 'EAE';
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS vigente BOOLEAN NOT NULL DEFAULT TRUE;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS estado_lista VARCHAR(50) NOT NULL DEFAULT 'LV_BORRADOR';
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS nombre_eae TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS numero_aoc_fecha_validez TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS direccion_estado_explotador TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS direccion_estado_reconocimiento TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS tipos_aeronaves TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS tipo_operacion TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS fecha_lista TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS inspector_responsable TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS cargo_inspector TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS resumen_verificacion TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS observaciones_generales TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS resultado_general VARCHAR(120) NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS items_json TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS ruta_pdf TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS ruta_documento_firmado TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS hash_documento VARCHAR(256) NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS finalizado BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS firmado_tecnico BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS fecha_finalizacion TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS fecha_firma TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS usuario_firma VARCHAR(250) NULL;

                    -- Índices
                    CREATE INDEX IF NOT EXISTS ix_aocr_tblv_eae_solicitud_estacion
                        ON public.aocr_tblv_operacional_eae(solicitud_id, estacion_id, codigo_inspeccion, version DESC);

                    CREATE INDEX IF NOT EXISTS ix_aocr_tblv_eae_vigente_lookup
                        ON public.aocr_tblv_operacional_eae(solicitud_id, COALESCE(estacion_id, 0), tipo_lista)
                        WHERE vigente = TRUE;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }
    }
}
