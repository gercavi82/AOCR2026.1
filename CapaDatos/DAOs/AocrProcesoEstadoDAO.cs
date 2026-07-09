using System;
using System.Configuration;
using CapaDatos.Models;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class AocrProcesoEstadoDAO
    {
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;
        private readonly string _connectionString;

        public AocrProcesoEstadoDAO()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                ?? ConexionDAO.CadenaConexion;
        }

        public AocrProcesoEstadoRecord ObtenerActivoPorSolicitud(int solicitudId)
        {
            if (solicitudId <= 0)
            {
                return null;
            }

            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT id, solicitud_id, orden_recaudacion_id, inspeccion_id, informe_id,
                           estado_actual, etapa_actual, rol_responsable, usuario_responsable_id,
                           siguiente_accion, observacion, fecha_estado, activo
                    FROM public.aocr_proceso_estado
                    WHERE solicitud_id = @solicitud_id
                      AND activo = TRUE
                    ORDER BY fecha_estado DESC, id DESC
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? MapEstado(rd) : null;
                    }
                }
            }
        }

        public System.Collections.Generic.List<AocrProcesoEstadoRecord> ListarActivosPorEstado(params string[] estados)
        {
            var list = new System.Collections.Generic.List<AocrProcesoEstadoRecord>();
            if (estados == null || estados.Length == 0)
            {
                return list;
            }

            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT id, solicitud_id, orden_recaudacion_id, inspeccion_id, informe_id,
                           estado_actual, etapa_actual, rol_responsable, usuario_responsable_id,
                           siguiente_accion, observacion, fecha_estado, activo
                    FROM public.aocr_proceso_estado
                    WHERE activo = TRUE
                      AND UPPER(COALESCE(estado_actual, '')) = ANY(@estados)
                    ORDER BY fecha_estado DESC, id DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    var normalizados = System.Linq.Enumerable.ToArray(
                        System.Linq.Enumerable.Select(
                            System.Linq.Enumerable.Where(estados, e => !string.IsNullOrWhiteSpace(e)),
                            e => e.Trim().ToUpperInvariant()));
                    cmd.Parameters.AddWithValue("@estados", normalizados);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(MapEstado(rd));
                        }
                    }
                }
            }

            return list;
        }

        public int UpsertEstadoActual(AocrProcesoEstadoRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                EnsureSchema(cn);
                return UpsertEstadoActual(cn, null, record);
            }
        }

        public int UpsertEstadoActual(NpgsqlConnection cn, NpgsqlTransaction tx, AocrProcesoEstadoRecord record)
        {
            if (cn == null)
            {
                throw new ArgumentNullException(nameof(cn));
            }

            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            EnsureSchema(cn);

            const string sqlDeactivate = @"
                UPDATE public.aocr_proceso_estado
                SET activo = FALSE
                WHERE solicitud_id = @solicitud_id
                  AND activo = TRUE;";

            using (var cmd = new NpgsqlCommand(sqlDeactivate, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud_id", record.SolicitudId);
                cmd.ExecuteNonQuery();
            }

            const string sqlInsert = @"
                INSERT INTO public.aocr_proceso_estado
                (
                    solicitud_id,
                    orden_recaudacion_id,
                    inspeccion_id,
                    informe_id,
                    estado_actual,
                    etapa_actual,
                    rol_responsable,
                    usuario_responsable_id,
                    siguiente_accion,
                    observacion,
                    fecha_estado,
                    activo
                )
                VALUES
                (
                    @solicitud_id,
                    @orden_recaudacion_id,
                    @inspeccion_id,
                    @informe_id,
                    @estado_actual,
                    @etapa_actual,
                    @rol_responsable,
                    @usuario_responsable_id,
                    @siguiente_accion,
                    @observacion,
                    COALESCE(@fecha_estado, NOW()),
                    TRUE
                )
                RETURNING id;";

            using (var cmd = new NpgsqlCommand(sqlInsert, cn, tx))
            {
                BindEstado(cmd, record);
                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        public int InsertarHistorial(AocrProcesoEstadoHistorialRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                EnsureSchema(cn);
                return InsertarHistorial(cn, null, record);
            }
        }

        public int InsertarHistorial(NpgsqlConnection cn, NpgsqlTransaction tx, AocrProcesoEstadoHistorialRecord record)
        {
            if (cn == null)
            {
                throw new ArgumentNullException(nameof(cn));
            }

            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            EnsureSchema(cn);

            const string sql = @"
                INSERT INTO public.aocr_proceso_estado_historial
                (
                    solicitud_id,
                    orden_recaudacion_id,
                    inspeccion_id,
                    informe_id,
                    estado_anterior,
                    estado_nuevo,
                    etapa,
                    accion,
                    rol_usuario,
                    usuario_id,
                    rol_responsable,
                    usuario_responsable_id,
                    observacion,
                    fecha_creacion
                )
                VALUES
                (
                    @solicitud_id,
                    @orden_recaudacion_id,
                    @inspeccion_id,
                    @informe_id,
                    @estado_anterior,
                    @estado_nuevo,
                    @etapa,
                    @accion,
                    @rol_usuario,
                    @usuario_id,
                    @rol_responsable,
                    @usuario_responsable_id,
                    @observacion,
                    COALESCE(@fecha_creacion, NOW())
                )
                RETURNING id;";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                BindHistorial(cmd, record);
                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        private static AocrProcesoEstadoRecord MapEstado(NpgsqlDataReader rd)
        {
            return new AocrProcesoEstadoRecord
            {
                Id = Convert.ToInt32(rd["id"]),
                SolicitudId = Convert.ToInt32(rd["solicitud_id"]),
                OrdenRecaudacionId = rd["orden_recaudacion_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["orden_recaudacion_id"]),
                InspeccionId = rd["inspeccion_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["inspeccion_id"]),
                InformeId = rd["informe_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["informe_id"]),
                EstadoActual = rd["estado_actual"] == DBNull.Value ? null : rd["estado_actual"].ToString(),
                EtapaActual = rd["etapa_actual"] == DBNull.Value ? null : rd["etapa_actual"].ToString(),
                RolResponsable = rd["rol_responsable"] == DBNull.Value ? null : rd["rol_responsable"].ToString(),
                UsuarioResponsableId = rd["usuario_responsable_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["usuario_responsable_id"]),
                SiguienteAccion = rd["siguiente_accion"] == DBNull.Value ? null : rd["siguiente_accion"].ToString(),
                Observacion = rd["observacion"] == DBNull.Value ? null : rd["observacion"].ToString(),
                FechaEstado = rd["fecha_estado"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(rd["fecha_estado"]),
                Activo = rd["activo"] != DBNull.Value && Convert.ToBoolean(rd["activo"])
            };
        }

        private static void BindEstado(NpgsqlCommand cmd, AocrProcesoEstadoRecord record)
        {
            cmd.Parameters.AddWithValue("@solicitud_id", record.SolicitudId);
            cmd.Parameters.AddWithValue("@orden_recaudacion_id", (object)record.OrdenRecaudacionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@inspeccion_id", (object)record.InspeccionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@informe_id", (object)record.InformeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@estado_actual", (object)record.EstadoActual ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@etapa_actual", (object)record.EtapaActual ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rol_responsable", (object)record.RolResponsable ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@usuario_responsable_id", (object)record.UsuarioResponsableId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@siguiente_accion", (object)record.SiguienteAccion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@observacion", (object)record.Observacion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fecha_estado", record.FechaEstado == DateTime.MinValue ? (object)DBNull.Value : record.FechaEstado);
        }

        private static void BindHistorial(NpgsqlCommand cmd, AocrProcesoEstadoHistorialRecord record)
        {
            cmd.Parameters.AddWithValue("@solicitud_id", record.SolicitudId);
            cmd.Parameters.AddWithValue("@orden_recaudacion_id", (object)record.OrdenRecaudacionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@inspeccion_id", (object)record.InspeccionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@informe_id", (object)record.InformeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@estado_anterior", (object)record.EstadoAnterior ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@estado_nuevo", (object)record.EstadoNuevo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@etapa", (object)record.Etapa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@accion", (object)record.Accion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rol_usuario", (object)record.RolUsuario ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@usuario_id", (object)record.UsuarioId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rol_responsable", (object)record.RolResponsable ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@usuario_responsable_id", (object)record.UsuarioResponsableId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@observacion", (object)record.Observacion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fecha_creacion", record.FechaCreacion == DateTime.MinValue ? (object)DBNull.Value : record.FechaCreacion);
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
                    CREATE TABLE IF NOT EXISTS public.aocr_proceso_estado
                    (
                        id SERIAL PRIMARY KEY,
                        solicitud_id INTEGER NOT NULL,
                        orden_recaudacion_id INTEGER NULL,
                        inspeccion_id INTEGER NULL,
                        informe_id INTEGER NULL,
                        estado_actual VARCHAR(100) NOT NULL,
                        etapa_actual VARCHAR(100) NULL,
                        rol_responsable VARCHAR(100) NULL,
                        usuario_responsable_id INTEGER NULL,
                        siguiente_accion VARCHAR(150) NULL,
                        observacion TEXT NULL,
                        fecha_estado TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                        activo BOOLEAN NOT NULL DEFAULT TRUE
                    );

                    CREATE INDEX IF NOT EXISTS ix_aocr_proceso_estado_solicitud
                        ON public.aocr_proceso_estado(solicitud_id);

                    CREATE INDEX IF NOT EXISTS ix_aocr_proceso_estado_estado
                        ON public.aocr_proceso_estado(estado_actual);

                    CREATE TABLE IF NOT EXISTS public.aocr_proceso_estado_historial
                    (
                        id SERIAL PRIMARY KEY,
                        solicitud_id INTEGER NOT NULL,
                        orden_recaudacion_id INTEGER NULL,
                        inspeccion_id INTEGER NULL,
                        informe_id INTEGER NULL,
                        estado_anterior VARCHAR(100) NULL,
                        estado_nuevo VARCHAR(100) NOT NULL,
                        etapa VARCHAR(150) NULL,
                        accion VARCHAR(150) NULL,
                        rol_usuario VARCHAR(100) NULL,
                        usuario_id INTEGER NULL,
                        rol_responsable VARCHAR(100) NULL,
                        usuario_responsable_id INTEGER NULL,
                        observacion TEXT NULL,
                        fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
                    );

                    CREATE INDEX IF NOT EXISTS ix_aocr_proceso_estado_historial_solicitud
                        ON public.aocr_proceso_estado_historial(solicitud_id);

                    ALTER TABLE public.aocr_proceso_estado_historial ADD COLUMN IF NOT EXISTS etapa VARCHAR(150) NULL;
                    ALTER TABLE public.aocr_proceso_estado_historial ADD COLUMN IF NOT EXISTS rol_responsable VARCHAR(100) NULL;
                    ALTER TABLE public.aocr_proceso_estado_historial ADD COLUMN IF NOT EXISTS usuario_responsable_id INTEGER NULL;
                ";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }

        public System.Collections.Generic.List<AocrProcesoEstadoHistorialRecord> ObtenerHistorialPorSolicitud(int solicitudId)
        {
            var list = new System.Collections.Generic.List<AocrProcesoEstadoHistorialRecord>();
            if (solicitudId <= 0) return list;

            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT h.id, h.solicitud_id, h.orden_recaudacion_id, h.inspeccion_id, h.informe_id,
                           h.estado_anterior, h.estado_nuevo, h.etapa, h.accion, h.rol_usuario, h.usuario_id,
                           h.rol_responsable, h.usuario_responsable_id, h.observacion, h.fecha_creacion,
                           TRIM(COALESCE(u.nombreusuario, '') || ' ' || COALESCE(u.apellidousuario, '')) AS usuario_nombre,
                           TRIM(COALESCE(ur.nombreusuario, '') || ' ' || COALESCE(ur.apellidousuario, '')) AS responsable_nombre
                    FROM public.aocr_proceso_estado_historial h
                    LEFT JOIN public.usuario u ON u.idusuario = h.usuario_id
                    LEFT JOIN public.usuario ur ON ur.idusuario = h.usuario_responsable_id
                    WHERE h.solicitud_id = @solicitud_id
                    ORDER BY h.fecha_creacion ASC, h.id ASC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var rec = new AocrProcesoEstadoHistorialRecord
                            {
                                Id = Convert.ToInt32(rd["id"]),
                                SolicitudId = Convert.ToInt32(rd["solicitud_id"]),
                                OrdenRecaudacionId = rd["orden_recaudacion_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["orden_recaudacion_id"]),
                                InspeccionId = rd["inspeccion_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["inspeccion_id"]),
                                InformeId = rd["informe_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["informe_id"]),
                                EstadoAnterior = rd["estado_anterior"] == DBNull.Value ? null : rd["estado_anterior"].ToString(),
                                EstadoNuevo = rd["estado_nuevo"] == DBNull.Value ? null : rd["estado_nuevo"].ToString(),
                                Etapa = rd["etapa"] == DBNull.Value ? null : rd["etapa"].ToString(),
                                Accion = rd["accion"] == DBNull.Value ? null : rd["accion"].ToString(),
                                RolUsuario = rd["rol_usuario"] == DBNull.Value ? null : rd["rol_usuario"].ToString(),
                                UsuarioId = rd["usuario_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["usuario_id"]),
                                RolResponsable = rd["rol_responsable"] == DBNull.Value ? null : rd["rol_responsable"].ToString(),
                                UsuarioResponsableId = rd["usuario_responsable_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["usuario_responsable_id"]),
                                Observacion = rd["observacion"] == DBNull.Value ? null : rd["observacion"].ToString(),
                                FechaCreacion = Convert.ToDateTime(rd["fecha_creacion"]),
                                UsuarioNombre = rd["usuario_nombre"] == DBNull.Value ? null : rd["usuario_nombre"].ToString(),
                                ResponsableNombre = rd["responsable_nombre"] == DBNull.Value ? null : rd["responsable_nombre"].ToString()
                            };

                            if (string.IsNullOrWhiteSpace(rec.UsuarioNombre) && rec.UsuarioId.HasValue)
                            {
                                rec.UsuarioNombre = "Usuario ID: " + rec.UsuarioId.Value;
                            }
                            if (string.IsNullOrWhiteSpace(rec.ResponsableNombre) && rec.UsuarioResponsableId.HasValue)
                            {
                                rec.ResponsableNombre = "Usuario ID: " + rec.UsuarioResponsableId.Value;
                            }

                            list.Add(rec);
                        }
                    }
                }
            }
            return list;
        }

        public int ResolverSolicitudIdPorOrden(int ordenId)
        {
            if (ordenId <= 0) return 0;
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                const string sql = "SELECT codigo_solicitud FROM public.aocr_or_orden WHERE id = @id LIMIT 1;";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", ordenId);
                    var scalar = cmd.ExecuteScalar();
                    if (scalar != null && scalar != DBNull.Value)
                    {
                        int id;
                        if (int.TryParse(scalar.ToString(), out id)) return id;
                    }
                }
            }
            return 0;
        }

        public int ResolverSolicitudIdPorInspeccion(int inspeccionId)
        {
            if (inspeccionId <= 0) return 0;
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                const string sql = "SELECT codigo_solicitud FROM public.aocr_tbinspeccion WHERE codigo_inspeccion = @id LIMIT 1;";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", inspeccionId);
                    var scalar = cmd.ExecuteScalar();
                    if (scalar != null && scalar != DBNull.Value)
                    {
                        int id;
                        if (int.TryParse(scalar.ToString(), out id)) return id;
                    }
                }
            }
            return 0;
        }

        public int ResolverSolicitudIdPorInforme(int informeId)
        {
            if (informeId <= 0) return 0;
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                const string sql = @"
                    SELECT i.codigo_solicitud 
                    FROM public.aocr_tbinforme_inspeccion inf
                    JOIN public.aocr_tbinspeccion i ON i.codigo_inspeccion = inf.codigo_inspeccion
                    WHERE inf.codigo_informe = @id 
                    LIMIT 1;";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", informeId);
                    var scalar = cmd.ExecuteScalar();
                    if (scalar != null && scalar != DBNull.Value)
                    {
                        int id;
                        if (int.TryParse(scalar.ToString(), out id)) return id;
                    }
                }
            }
            return 0;
        }
    }
}
