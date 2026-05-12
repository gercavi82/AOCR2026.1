using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Npgsql;
using CapaDatos.Constants;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class InspeccionDAO
    {
        private readonly string _cs;
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;

        public InspeccionDAO()
        {
            var settings = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _cs = (settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString))
                ? settings.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public Inspeccion ObtenerPorId(int id)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                var columnasSolicitud = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
                var columnasSelectSolicitud = new[]
                {
                    SelectSolicitudColumn(columnasSolicitud, "codigo_tecnico", "solicitud_codigo_tecnico", "integer"),
                    SelectSolicitudColumn(columnasSolicitud, "tecnico_responsable_cedula", "solicitud_inspector_principal_cedula"),
                    SelectSolicitudColumn(columnasSolicitud, "tecnico_responsable_nombre", "solicitud_inspector_principal_nombre"),
                    SelectSolicitudColumn(columnasSolicitud, "tecnico_responsable_tipo", "solicitud_inspector_principal_tipo"),
                    SelectSolicitudColumn(columnasSolicitud, "inspector_apoyo_cedula", "solicitud_inspector_apoyo_cedula"),
                    SelectSolicitudColumn(columnasSolicitud, "inspector_apoyo_nombre", "solicitud_inspector_apoyo_nombre"),
                    SelectSolicitudColumn(columnasSolicitud, "inspector_apoyo_tipo", "solicitud_inspector_apoyo_tipo")
                };

                var sql = @"
                SELECT i.*, " + string.Join(", ", columnasSelectSolicitud) + @"
                FROM public.aocr_tbinspeccion i
                LEFT JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud = i.codigo_solicitud
                WHERE i.codigo_inspeccion = @id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return null;
                        return Map(dr);
                    }
                }
            }
        }

        public List<Inspeccion> ListarTodas()
        {
            var lista = new List<Inspeccion>();

            const string sql = @"
                SELECT *
                FROM public.aocr_tbinspeccion
                ORDER BY codigo_inspeccion DESC;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
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

        public List<Inspeccion> ListarPorInspector(int codigoInspector)
        {
            var lista = new List<Inspeccion>();

            const string sql = @"
                SELECT *
                FROM public.aocr_tbinspeccion
                WHERE codigo_inspector = @ci
                ORDER BY fecha_programada DESC NULLS LAST, codigo_inspeccion DESC;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@ci", codigoInspector);
                cn.Open();

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

        public List<Inspeccion> ListarPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<Inspeccion>();

            const string sql = @"
                SELECT *
                FROM public.aocr_tbinspeccion
                WHERE codigo_solicitud = @cs
                ORDER BY fecha_programada DESC NULLS LAST, codigo_inspeccion DESC;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@cs", codigoSolicitud);
                cn.Open();

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

        public int Crear(Inspeccion i)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                var columnas = ObtenerColumnasTabla(cn, "aocr_tbinspeccion");

                var numeroInspeccion = string.IsNullOrWhiteSpace(i.NumeroInspeccion)
                    ? GenerarNumeroInspeccion(cn, i.CodigoSolicitud)
                    : i.NumeroInspeccion.Trim();
                var tipoCodigo = ResolverTipoCodigo(i);
                var estado = ResolverEstadoPersistencia(cn, i.Estado);
                var fechaProgramada = (i.FechaProgramada ?? DateTime.Today).Date;
                var actor = ((i.CreatedBy ?? i.UpdatedBy) ?? 0).ToString();

                var cols = new List<string>();
                var vals = new List<string>();

                cols.Add("codigo_solicitud");
                vals.Add("@sol");

                if (columnas.Contains("numero_inspeccion"))
                {
                    cols.Add("numero_inspeccion");
                    vals.Add("@num");
                }

                if (columnas.Contains("tipo"))
                {
                    cols.Add("tipo");
                    vals.Add("@tipo");
                }

                if (columnas.Contains("fecha_programada"))
                {
                    cols.Add("fecha_programada");
                    vals.Add("@fp");
                }

                if (columnas.Contains("codigo_inspector"))
                {
                    cols.Add("codigo_inspector");
                    vals.Add("@insp");
                }

                if (columnas.Contains("inspector_principal_cedula"))
                {
                    cols.Add("inspector_principal_cedula");
                    vals.Add("@inspector_principal_cedula");
                }

                if (columnas.Contains("inspector_principal_nombre"))
                {
                    cols.Add("inspector_principal_nombre");
                    vals.Add("@inspector_principal_nombre");
                }

                if (columnas.Contains("inspector_principal_tipo"))
                {
                    cols.Add("inspector_principal_tipo");
                    vals.Add("@inspector_principal_tipo");
                }

                if (columnas.Contains("inspector_apoyo_cedula"))
                {
                    cols.Add("inspector_apoyo_cedula");
                    vals.Add("@inspector_apoyo_cedula");
                }

                if (columnas.Contains("inspector_apoyo_nombre"))
                {
                    cols.Add("inspector_apoyo_nombre");
                    vals.Add("@inspector_apoyo_nombre");
                }

                if (columnas.Contains("inspector_apoyo_tipo"))
                {
                    cols.Add("inspector_apoyo_tipo");
                    vals.Add("@inspector_apoyo_tipo");
                }

                if (columnas.Contains("estado"))
                {
                    cols.Add("estado");
                    vals.Add("@estado");
                }

                if (columnas.Contains("resultado"))
                {
                    cols.Add("resultado");
                    vals.Add("@res");
                }

                if (columnas.Contains("comentarios"))
                {
                    cols.Add("comentarios");
                    vals.Add("@comentarios");
                }

                if (columnas.Contains("observaciones_generales"))
                {
                    cols.Add("observaciones_generales");
                    vals.Add("@observaciones_generales");
                }

                if (columnas.Contains("hallazgos_principales"))
                {
                    cols.Add("hallazgos_principales");
                    vals.Add("@hallazgos_principales");
                }

                if (columnas.Contains("viaticos_requeridos"))
                {
                    cols.Add("viaticos_requeridos");
                    vals.Add("@viaticos_requeridos");
                }

                if (columnas.Contains("viaticos_monto"))
                {
                    cols.Add("viaticos_monto");
                    vals.Add("@viaticos_monto");
                }

                if (columnas.Contains("pago_viaticos_validado"))
                {
                    cols.Add("pago_viaticos_validado");
                    vals.Add("@pago_viaticos_validado");
                }

                if (columnas.Contains("fecha_pago_viaticos"))
                {
                    cols.Add("fecha_pago_viaticos");
                    vals.Add("@fecha_pago_viaticos");
                }

                if (columnas.Contains("estado_documental"))
                {
                    cols.Add("estado_documental");
                    vals.Add("@estado_documental");
                }

                if (columnas.Contains("resultado_evaluacion"))
                {
                    cols.Add("resultado_evaluacion");
                    vals.Add("@resultado_evaluacion");
                }

                if (columnas.Contains("created_at"))
                {
                    cols.Add("created_at");
                    vals.Add("NOW()");
                }

                if (columnas.Contains("created_by"))
                {
                    cols.Add("created_by");
                    vals.Add("@cby");
                }

                if (columnas.Contains("updated_at"))
                {
                    cols.Add("updated_at");
                    vals.Add("NOW()");
                }

                if (columnas.Contains("updated_by"))
                {
                    cols.Add("updated_by");
                    vals.Add("@uby");
                }

                var sql = "INSERT INTO public.aocr_tbinspeccion (" +
                          string.Join(", ", cols) +
                          ") VALUES (" +
                          string.Join(", ", vals) +
                          ") RETURNING codigo_inspeccion;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@sol", i.CodigoSolicitud);
                    cmd.Parameters.AddWithValue("@num", (object)numeroInspeccion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo", tipoCodigo);
                    cmd.Parameters.AddWithValue("@fp", fechaProgramada);
                    cmd.Parameters.AddWithValue("@insp", (object)i.CodigoInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_principal_cedula", (object)i.InspectorPrincipalCedula ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_principal_nombre", (object)i.InspectorPrincipalNombre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_principal_tipo", (object)i.InspectorPrincipalTipo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_apoyo_cedula", (object)i.InspectorApoyoCedula ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_apoyo_nombre", (object)i.InspectorApoyoNombre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_apoyo_tipo", (object)i.InspectorApoyoTipo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado", estado);
                    cmd.Parameters.AddWithValue("@res", (object)i.Resultado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@comentarios", (object)i.Comentarios ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observaciones_generales", (object)i.ObservacionesGenerales ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hallazgos_principales", (object)i.HallazgosPrincipales ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@viaticos_requeridos", i.ViaticosRequeridos);
                    cmd.Parameters.AddWithValue("@viaticos_monto", (object)i.ViaticosMonto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pago_viaticos_validado", i.PagoViaticosValidado);
                    cmd.Parameters.AddWithValue("@fecha_pago_viaticos", (object)i.FechaPagoViaticos ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado_documental", (object)i.EstadoDocumental ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@resultado_evaluacion", (object)i.ResultadoEvaluacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cby", (object)actor ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@uby", (object)actor ?? DBNull.Value);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public bool Actualizar(Inspeccion i)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                var columnas = ObtenerColumnasTabla(cn, "aocr_tbinspeccion");
                var set = new List<string>();

                if (columnas.Contains("codigo_inspector")) set.Add("codigo_inspector = @insp");
                if (columnas.Contains("inspector_principal_cedula")) set.Add("inspector_principal_cedula = @inspector_principal_cedula");
                if (columnas.Contains("inspector_principal_nombre")) set.Add("inspector_principal_nombre = @inspector_principal_nombre");
                if (columnas.Contains("inspector_principal_tipo")) set.Add("inspector_principal_tipo = @inspector_principal_tipo");
                if (columnas.Contains("inspector_apoyo_cedula")) set.Add("inspector_apoyo_cedula = @inspector_apoyo_cedula");
                if (columnas.Contains("inspector_apoyo_nombre")) set.Add("inspector_apoyo_nombre = @inspector_apoyo_nombre");
                if (columnas.Contains("inspector_apoyo_tipo")) set.Add("inspector_apoyo_tipo = @inspector_apoyo_tipo");
                if (columnas.Contains("fecha_programada")) set.Add("fecha_programada = @fp");
                if (columnas.Contains("hora_programada")) set.Add("hora_programada = @hora_programada");
                if (columnas.Contains("tipo")) set.Add("tipo = @tipo");
                if (columnas.Contains("estado")) set.Add("estado = @estado");
                if (columnas.Contains("resultado")) set.Add("resultado = @res");
                if (columnas.Contains("comentarios")) set.Add("comentarios = @comentarios");
                if (columnas.Contains("observaciones_generales")) set.Add("observaciones_generales = @observaciones_generales");
                if (columnas.Contains("hallazgos_principales")) set.Add("hallazgos_principales = @hallazgos_principales");
                if (columnas.Contains("viaticos_requeridos")) set.Add("viaticos_requeridos = @viaticos_requeridos");
                if (columnas.Contains("viaticos_monto")) set.Add("viaticos_monto = @viaticos_monto");
                if (columnas.Contains("pago_viaticos_validado")) set.Add("pago_viaticos_validado = @pago_viaticos_validado");
                if (columnas.Contains("fecha_pago_viaticos")) set.Add("fecha_pago_viaticos = @fecha_pago_viaticos");
                if (columnas.Contains("estado_documental")) set.Add("estado_documental = @estado_documental");
                if (columnas.Contains("resultado_evaluacion")) set.Add("resultado_evaluacion = @resultado_evaluacion");
                if (columnas.Contains("updated_at")) set.Add("updated_at = NOW()");
                if (columnas.Contains("updated_by")) set.Add("updated_by = @uby");

                if (set.Count == 0)
                {
                    return false;
                }

                var sql = "UPDATE public.aocr_tbinspeccion SET " +
                          string.Join(", ", set) +
                          " WHERE codigo_inspeccion = @id;";
                var estadoPersistencia = ResolverEstadoPersistencia(cn, i.Estado);

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", i.CodigoInspeccion);
                    cmd.Parameters.AddWithValue("@insp", (object)i.CodigoInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_principal_cedula", (object)i.InspectorPrincipalCedula ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_principal_nombre", (object)i.InspectorPrincipalNombre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_principal_tipo", (object)i.InspectorPrincipalTipo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_apoyo_cedula", (object)i.InspectorApoyoCedula ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_apoyo_nombre", (object)i.InspectorApoyoNombre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inspector_apoyo_tipo", (object)i.InspectorApoyoTipo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fp", (object)i.FechaProgramada ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hora_programada", (object)i.HoraProgramada ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo", ResolverTipoCodigo(i));
                    cmd.Parameters.AddWithValue("@estado", estadoPersistencia);
                    cmd.Parameters.AddWithValue("@res", (object)i.Resultado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@comentarios", (object)i.Comentarios ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observaciones_generales", (object)i.ObservacionesGenerales ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hallazgos_principales", (object)i.HallazgosPrincipales ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@viaticos_requeridos", i.ViaticosRequeridos);
                    cmd.Parameters.AddWithValue("@viaticos_monto", (object)i.ViaticosMonto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pago_viaticos_validado", i.PagoViaticosValidado);
                    cmd.Parameters.AddWithValue("@fecha_pago_viaticos", (object)i.FechaPagoViaticos ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado_documental", (object)i.EstadoDocumental ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@resultado_evaluacion", (object)i.ResultadoEvaluacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@uby", (object)(((i.UpdatedBy ?? i.CreatedBy) ?? 0).ToString()) ?? DBNull.Value);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool CambiarEstado(int id, string estado, int updatedBy)
        {
            const string sql = @"
                UPDATE public.aocr_tbinspeccion
                SET estado = @estado, updated_at = NOW(), updated_by = @uby
                WHERE codigo_inspeccion = @id;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                var estadoNormalizado = ResolverEstadoPersistencia(cn, estado);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@estado", estadoNormalizado);
                cmd.Parameters.AddWithValue("@uby", updatedBy.ToString());

                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool GuardarInforme(int id, string rutaInforme, int updatedBy)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                var columnas = ObtenerColumnasTabla(cn, "aocr_tbinspeccion");
                var columnaInforme = columnas.Contains("informe_pdf")
                    ? "informe_pdf"
                    : (columnas.Contains("ruta_informe") ? "ruta_informe" : null);

                if (string.IsNullOrWhiteSpace(columnaInforme))
                {
                    return false;
                }

                var set = new List<string>
                {
                    columnaInforme + " = @ruta"
                };

                if (columnas.Contains("updated_at")) set.Add("updated_at = NOW()");
                if (columnas.Contains("updated_by")) set.Add("updated_by = @uby");

                var sql = "UPDATE public.aocr_tbinspeccion SET " +
                          string.Join(", ", set) +
                          " WHERE codigo_inspeccion = @id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@ruta", (object)rutaInforme ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@uby", updatedBy.ToString());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool Cerrar(int id, string resultado, int updatedBy)
        {
            const string sql = @"
                UPDATE public.aocr_tbinspeccion
                SET estado = 'CERRADA',
                    resultado = @res,
                    updated_at = NOW(),
                    updated_by = @uby
                WHERE codigo_inspeccion = @id;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@res", (object)resultado ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uby", updatedBy.ToString());

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private Inspeccion Map(IDataRecord dr)
        {
            object value;

            var tipoCodigo = TryGetValue(dr, "tipo", out value) ? SafeToNullableInt(value) : null;
            var codigoInspector = TryGetValue(dr, "codigo_inspector", out value) ? SafeToNullableInt(value) : null;
            if (!codigoInspector.HasValue && TryGetValue(dr, "solicitud_codigo_tecnico", out value))
            {
                codigoInspector = SafeToNullableInt(value);
            }

            var inspectorPrincipalCedula = FirstNonEmpty(
                TryGetValue(dr, "inspector_principal_cedula", out value) ? value.ToString() : null,
                TryGetValue(dr, "solicitud_inspector_principal_cedula", out value) ? value.ToString() : null);
            var inspectorPrincipalNombre = FirstNonEmpty(
                TryGetValue(dr, "inspector_principal_nombre", out value) ? value.ToString() : null,
                TryGetValue(dr, "solicitud_inspector_principal_nombre", out value) ? value.ToString() : null);
            var inspectorPrincipalTipo = FirstNonEmpty(
                TryGetValue(dr, "inspector_principal_tipo", out value) ? value.ToString() : null,
                TryGetValue(dr, "solicitud_inspector_principal_tipo", out value) ? value.ToString() : null);
            var inspectorApoyoCedula = FirstNonEmpty(
                TryGetValue(dr, "inspector_apoyo_cedula", out value) ? value.ToString() : null,
                TryGetValue(dr, "solicitud_inspector_apoyo_cedula", out value) ? value.ToString() : null);
            var inspectorApoyoNombre = FirstNonEmpty(
                TryGetValue(dr, "inspector_apoyo_nombre", out value) ? value.ToString() : null,
                TryGetValue(dr, "solicitud_inspector_apoyo_nombre", out value) ? value.ToString() : null);
            var inspectorApoyoTipo = FirstNonEmpty(
                TryGetValue(dr, "inspector_apoyo_tipo", out value) ? value.ToString() : null,
                TryGetValue(dr, "solicitud_inspector_apoyo_tipo", out value) ? value.ToString() : null);

            return new Inspeccion
            {
                CodigoInspeccion = TryGetValue(dr, "codigo_inspeccion", out value) ? SafeToInt(value) : 0,
                CodigoSolicitud = TryGetValue(dr, "codigo_solicitud", out value) ? SafeToInt(value) : 0,
                NumeroInspeccion = TryGetValue(dr, "numero_inspeccion", out value) ? value.ToString() : null,
                CodigoInspector = codigoInspector,
                FechaProgramada = TryGetValue(dr, "fecha_programada", out value) ? SafeToNullableDate(value) : null,
                HoraProgramada = TryGetValue(dr, "hora_programada", out value) ? SafeToNullableTime(value) : null,
                TipoCodigo = tipoCodigo,
                Tipo = MapearTipoCodigo(tipoCodigo, TryGetValue(dr, "tipo", out value) ? value.ToString() : null),
                Lugar = TryGetValue(dr, "lugar", out value) ? value.ToString() : null,
                Estado = TryGetValue(dr, "estado", out value) ? EstadosInspeccion.NormalizarEstado(value.ToString()) : null,
                Resultado = TryGetValue(dr, "resultado", out value) ? value.ToString() : null,
                Comentarios = TryGetValue(dr, "comentarios", out value) ? value.ToString() : null,
                ObservacionesGenerales = TryGetValue(dr, "observaciones_generales", out value) ? value.ToString() : null,
                HallazgosPrincipales = TryGetValue(dr, "hallazgos_principales", out value) ? value.ToString() : null,
                ViaticosRequeridos = TryGetValue(dr, "viaticos_requeridos", out value) && SafeToBool(value),
                ViaticosMonto = TryGetValue(dr, "viaticos_monto", out value) ? SafeToNullableDecimal(value) : null,
                PagoViaticosValidado = TryGetValue(dr, "pago_viaticos_validado", out value) && SafeToBool(value),
                FechaPagoViaticos = TryGetValue(dr, "fecha_pago_viaticos", out value) ? SafeToNullableDate(value) : null,
                EstadoDocumental = TryGetValue(dr, "estado_documental", out value) ? value.ToString() : null,
                ResultadoEvaluacion = TryGetValue(dr, "resultado_evaluacion", out value) ? value.ToString() : null,
                RutaInforme = FirstNonEmpty(
                    TryGetValue(dr, "informe_pdf", out value) ? value.ToString() : null,
                    TryGetValue(dr, "ruta_informe", out value) ? value.ToString() : null),
                CreatedAt = TryGetValue(dr, "created_at", out value) ? SafeToNullableDate(value) : null,
                CreatedBy = TryGetValue(dr, "created_by", out value) ? SafeToNullableInt(value) : null,
                UpdatedAt = TryGetValue(dr, "updated_at", out value) ? SafeToNullableDate(value) : null,
                UpdatedBy = TryGetValue(dr, "updated_by", out value) ? SafeToNullableInt(value) : null,
                InspectorPrincipalCedula = inspectorPrincipalCedula,
                InspectorPrincipalNombre = inspectorPrincipalNombre,
                InspectorPrincipalTipo = inspectorPrincipalTipo,
                InspectorApoyoCedula = inspectorApoyoCedula,
                InspectorApoyoNombre = inspectorApoyoNombre,
                InspectorApoyoTipo = inspectorApoyoTipo
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
                    ALTER TABLE IF EXISTS public.aocr_tbinspeccion ADD COLUMN IF NOT EXISTS estado_documental VARCHAR(50);
                    ALTER TABLE IF EXISTS public.aocr_tbinspeccion ADD COLUMN IF NOT EXISTS resultado_evaluacion VARCHAR(50);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }

        private static HashSet<string> ObtenerColumnasTabla(NpgsqlConnection cn, string tabla)
        {
            if (string.Equals(tabla, "aocr_tbinspeccion", StringComparison.OrdinalIgnoreCase))
            {
                EnsureSchema(cn);
            }

            var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string sql = @"
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @tabla;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (!rd.IsDBNull(0))
                        {
                            columnas.Add(rd.GetString(0));
                        }
                    }
                }
            }

            return columnas;
        }

        private static string SelectSolicitudColumn(HashSet<string> columnas, string columnName, string alias, string nullCast = "text")
        {
            return columnas.Contains(columnName)
                ? $"s.{columnName} AS {alias}"
            : $"NULL::{nullCast} AS {alias}";
        }

        private static bool TryGetValue(IDataRecord rd, string column, out object value)
        {
            value = null;
            for (var i = 0; i < rd.FieldCount; i++)
            {
                if (!string.Equals(rd.GetName(i), column, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (rd.IsDBNull(i))
                {
                    return false;
                }

                value = rd.GetValue(i);
                return true;
            }

            return false;
        }

        private static int SafeToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            int parsed;
            return int.TryParse(value.ToString(), out parsed) ? parsed : 0;
        }

        private static int? SafeToNullableInt(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            int parsed;
            return int.TryParse(value.ToString(), out parsed) ? (int?)parsed : null;
        }

        private static decimal? SafeToNullableDecimal(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            decimal parsed;
            return decimal.TryParse(value.ToString(), out parsed) ? (decimal?)parsed : null;
        }

        private static bool SafeToBool(object value)
        {
            if (value == null || value == DBNull.Value) return false;

            var text = value.ToString();
            if (string.Equals(text, "1", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(text, "0", StringComparison.OrdinalIgnoreCase)) return false;

            bool parsed;
            return bool.TryParse(text, out parsed) && parsed;
        }

        private static DateTime? SafeToNullableDate(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            DateTime parsed;
            return DateTime.TryParse(value.ToString(), out parsed) ? (DateTime?)parsed : null;
        }

        private static TimeSpan? SafeToNullableTime(object value)
        {
            if (value == null || value == DBNull.Value) return null;

            if (value is TimeSpan)
            {
                return (TimeSpan)value;
            }

            TimeSpan parsed;
            return TimeSpan.TryParse(value.ToString(), out parsed) ? (TimeSpan?)parsed : null;
        }

        private static string NormalizarEstadoInspeccion(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return "CREADA";
            }

            var normalized = estado.Trim().ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U");

            switch (normalized)
            {
                case "PROGRAMADA":
                case "CREADA":
                case "EN_CURSO":
                case "APLAZADA":
                case "FINALIZADA":
                case "APROBADA":
                case "RECHAZADA":
                case "CANCELADA":
                case "CERRADA":
                    return normalized;
                case "EN PROGRESO":
                case "EN_PROGRESO":
                    return "EN_CURSO";
                default:
                    return "CREADA";
            }
        }

        private static string ResolverEstadoPersistencia(NpgsqlConnection cn, string estado)
        {
            var estadoCanonico = EstadosInspeccion.NormalizarEstado(estado);
            var permitidos = ObtenerEstadosPermitidosConstraint(cn);

            if (permitidos.Count == 0)
            {
                return estadoCanonico;
            }

            var estadoCanonicoPermitido = BuscarValorPermitido(permitidos, estadoCanonico);
            if (!string.IsNullOrWhiteSpace(estadoCanonicoPermitido))
            {
                return estadoCanonicoPermitido;
            }

            var estadoCore = EstadosInspeccion.MapearEstadoCoreCompat(estadoCanonico);
            var estadoCorePermitido = BuscarValorPermitido(permitidos, estadoCore);
            if (!string.IsNullOrWhiteSpace(estadoCorePermitido))
            {
                return estadoCorePermitido;
            }

            foreach (var permitido in permitidos)
            {
                if (string.Equals(EstadosInspeccion.NormalizarEstado(permitido), estadoCanonico, StringComparison.OrdinalIgnoreCase))
                {
                    return permitido;
                }
            }

            // No forzar un fallback arbitrario (permitidos.First) para no desalinear
            // el estado solicitado con el estado persistido cuando el constraint no coincide.
            return estadoCanonico;
        }

        private static HashSet<string> ObtenerEstadosPermitidosConstraint(NpgsqlConnection cn)
        {
            const string sql = @"
                SELECT pg_get_constraintdef(c.oid)
                FROM pg_constraint c
                JOIN pg_class t ON t.oid = c.conrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = 'public'
                  AND t.relname = 'aocr_tbinspeccion'
                  AND c.contype = 'c'
                  AND (
                        c.conname = 'chk_estado_inspeccion'
                        OR c.conname = 'chk_aocr_tbinspeccion_estado'
                        OR pg_get_constraintdef(c.oid) ILIKE '%estado%'
                      )
                ORDER BY CASE
                    WHEN c.conname = 'chk_estado_inspeccion' THEN 0
                    WHEN c.conname = 'chk_aocr_tbinspeccion_estado' THEN 1
                    ELSE 2
                END
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                var def = cmd.ExecuteScalar() as string;
                if (string.IsNullOrWhiteSpace(def))
                {
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match match in Regex.Matches(def, "'([^']+)'"))
                {
                    var value = match.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value.Trim());
                    }
                }

                return values;
            }
        }

        private static string BuscarValorPermitido(IEnumerable<string> permitidos, string candidato)
        {
            if (permitidos == null || string.IsNullOrWhiteSpace(candidato))
            {
                return null;
            }

            var candidatoNormalizado = NormalizarValorComparacion(candidato);

            foreach (var permitido in permitidos)
            {
                if (string.Equals(permitido, candidato, StringComparison.OrdinalIgnoreCase))
                {
                    return permitido;
                }

                if (string.Equals(NormalizarValorComparacion(permitido), candidatoNormalizado, StringComparison.OrdinalIgnoreCase))
                {
                    return permitido;
                }
            }

            return null;
        }

        private static string NormalizarValorComparacion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U")
                .Replace("-", "_")
                .Replace(" ", "_");
        }

        private static int ResolverTipoCodigo(Inspeccion model)
        {
            if (model != null && model.TipoCodigo.HasValue && model.TipoCodigo.Value > 0)
            {
                return model.TipoCodigo.Value;
            }

            var tipoTexto = (model != null ? model.Tipo : null) ?? string.Empty;
            var normalized = tipoTexto.Trim().ToUpperInvariant();
            int parsed;
            if (int.TryParse(normalized, out parsed) && parsed > 0)
            {
                return parsed;
            }

            switch (normalized)
            {
                case "INICIAL":
                    return 1;
                case "RENOVACION":
                case "RENOVACIÓN":
                    return 2;
                case "SEGUIMIENTO":
                    return 3;
                case "EXTRAORDINARIA":
                    return 4;
                default:
                    return 1;
            }
        }

        private static string MapearTipoCodigo(int? tipoCodigo, string raw)
        {
            if (!tipoCodigo.HasValue)
            {
                return raw;
            }

            switch (tipoCodigo.Value)
            {
                case 1: return "Inicial";
                case 2: return "Renovacion";
                case 3: return "Seguimiento";
                case 4: return "Extraordinaria";
                default: return tipoCodigo.Value.ToString();
            }
        }

        private static string GenerarNumeroInspeccion(NpgsqlConnection cn, int codigoSolicitud)
        {
            var referenciaSolicitud = ObtenerReferenciaSolicitudParaInspeccion(cn, codigoSolicitud);
            var baseNumero = ConstruirNumeroInspeccionBase(referenciaSolicitud, DateTime.Now);
            var candidato = baseNumero;

            for (var intento = 0; intento < 20; intento++)
            {
                using (var cmd = new NpgsqlCommand("SELECT 1 FROM aocr_tbinspeccion WHERE numero_inspeccion=@numero LIMIT 1;", cn))
                {
                    cmd.Parameters.AddWithValue("@numero", candidato);
                    var existe = cmd.ExecuteScalar();
                    if (existe == null || existe == DBNull.Value)
                    {
                        return candidato;
                    }
                }

                candidato = baseNumero + "-" + (intento + 2).ToString("D2");
            }

            return baseNumero + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        }

        private static string ObtenerReferenciaSolicitudParaInspeccion(NpgsqlConnection cn, int codigoSolicitud)
        {
            if (cn == null || codigoSolicitud <= 0)
            {
                return codigoSolicitud > 0 ? "AOCR" + codigoSolicitud.ToString() : "AOCR";
            }

            try
            {
                const string sql = @"
SELECT COALESCE(NULLIF(TRIM(numero_solicitud), ''), codigo_solicitud::text)
FROM aocr_tbsolicitud
WHERE codigo_solicitud = @codigoSolicitud
LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                    var numeroSolicitud = cmd.ExecuteScalar();
                    return CompactarNumeroSolicitudParaInspeccion(
                        numeroSolicitud != null && numeroSolicitud != DBNull.Value ? numeroSolicitud.ToString() : null,
                        codigoSolicitud);
                }
            }
            catch
            {
                return CompactarNumeroSolicitudParaInspeccion(null, codigoSolicitud);
            }
        }

        private static string CompactarNumeroSolicitudParaInspeccion(string numeroSolicitud, int codigoSolicitud)
        {
            var normalizado = string.IsNullOrWhiteSpace(numeroSolicitud)
                ? string.Empty
                : Regex.Replace(numeroSolicitud.Trim().ToUpperInvariant(), @"\s+", string.Empty);

            var coincidencia = !string.IsNullOrWhiteSpace(normalizado)
                ? Regex.Match(normalizado, @"AOCR\d+")
                : Match.Empty;

            if (coincidencia.Success)
            {
                return coincidencia.Value;
            }

            return codigoSolicitud > 0 ? "AOCR" + codigoSolicitud.ToString() : "AOCR";
        }

        private static string ConstruirNumeroInspeccionBase(string referenciaSolicitud, DateTime fechaReferencia)
        {
            var referenciaNormalizada = string.IsNullOrWhiteSpace(referenciaSolicitud)
                ? "AOCR"
                : Regex.Replace(referenciaSolicitud.Trim().ToUpperInvariant(), @"[^A-Z0-9]+", string.Empty);

            return "DGAC-INS-" + fechaReferencia.Year + "-" + referenciaNormalizada;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first.Trim();
            }

            if (!string.IsNullOrWhiteSpace(second))
            {
                return second.Trim();
            }

            return null;
        }
    }
}
