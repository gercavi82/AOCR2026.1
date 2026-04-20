using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using CapaPresentacion.Models.ViewModels;
using Npgsql;

namespace CapaPresentacion.Services
{
    public class InspectorDashboardService
    {
        private string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"];
                if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                {
                    throw new InvalidOperationException("No existe AOCRConnection en configuración.");
                }

                return cs.ConnectionString;
            }
        }

        public InspectorDashboardViewModel ObtenerDashboard(
            int codigoInspector,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string estado,
            string compania,
            int? codigoSolicitud)
        {
            if (codigoInspector <= 0)
            {
                throw new ArgumentException("Código de inspector inválido.");
            }

            var vm = new InspectorDashboardViewModel
            {
                CodigoInspector = codigoInspector,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta,
                Estado = estado,
                Compania = compania,
                CodigoSolicitud = codigoSolicitud
            };

            try
            {
                using (var cn = new NpgsqlConnection(ConnectionString))
                {
                    cn.Open();

                    var hasCreatedAt = ExisteColumna(cn, "aocr_tbinspeccion", "created_at");
                    var hasUpdatedAt = ExisteColumna(cn, "aocr_tbinspeccion", "updated_at");
                    var hasFechaProgramada = ExisteColumna(cn, "aocr_tbinspeccion", "fecha_programada");
                    var hasNumeroInspeccion = ExisteColumna(cn, "aocr_tbinspeccion", "numero_inspeccion");
                    var hasNombreOperador = ExisteColumna(cn, "aocr_tbsolicitud", "nombre_operador");
                    var hasCodigoOaci = ExisteColumna(cn, "aocr_tbsolicitud", "codigo_oaci");

                    var createdAtExpr = hasCreatedAt ? "i.created_at" : "NULL::timestamp";
                    var updatedAtExpr = hasUpdatedAt ? "i.updated_at" : "NULL::timestamp";
                    var fechaFiltroCol = hasFechaProgramada ? "i.fecha_programada" : (hasCreatedAt ? "i.created_at" : null);
                    var fechaSelectExpr = hasFechaProgramada ? "i.fecha_programada" : (hasCreatedAt ? "i.created_at" : "NULL::timestamp");
                    var numeroInspeccionExpr = hasNumeroInspeccion
                        ? "COALESCE(i.numero_inspeccion, ('INSP-' || i.codigo_inspeccion::text))"
                        : "('INSP-' || i.codigo_inspeccion::text)";
                    var operadorExpr = hasNombreOperador
                        ? "COALESCE(s.nombre_operador, '')"
                        : (hasCodigoOaci ? "COALESCE(s.codigo_oaci, '')" : "''");

                    var filtroFechaDesde = fechaFiltroCol != null
                        ? "(@fechaDesde IS NULL OR " + fechaFiltroCol + " >= @fechaDesde)"
                        : "(@fechaDesde IS NULL)";
                    var filtroFechaHasta = fechaFiltroCol != null
                        ? "(@fechaHasta IS NULL OR " + fechaFiltroCol + "::date <= @fechaHasta::date)"
                        : "(@fechaHasta IS NULL)";

                    string filtroCompania;
                    if (hasNombreOperador && hasCodigoOaci)
                    {
                        filtroCompania = "(@compania IS NULL OR UPPER(COALESCE(s.codigo_oaci, '')) LIKE UPPER(@compania) OR UPPER(COALESCE(s.nombre_operador, '')) LIKE UPPER(@compania))";
                    }
                    else if (hasNombreOperador)
                    {
                        filtroCompania = "(@compania IS NULL OR UPPER(COALESCE(s.nombre_operador, '')) LIKE UPPER(@compania))";
                    }
                    else if (hasCodigoOaci)
                    {
                        filtroCompania = "(@compania IS NULL OR UPPER(COALESCE(s.codigo_oaci, '')) LIKE UPPER(@compania))";
                    }
                    else
                    {
                        filtroCompania = "(@compania IS NULL)";
                    }

                    var sqlResumen = @"
WITH base AS (
    SELECT i.codigo_inspeccion,
           i.codigo_solicitud,
           COALESCE(i.estado, '') AS estado,
           " + createdAtExpr + @" AS created_at,
           " + updatedAtExpr + @" AS updated_at,
           " + fechaSelectExpr + @" AS fecha_programada
    FROM aocr_tbinspeccion i
    LEFT JOIN aocr_tbsolicitud s ON s.codigo_solicitud = i.codigo_solicitud
    WHERE i.codigo_inspector = @codigoInspector
      AND " + filtroFechaDesde + @"
      AND " + filtroFechaHasta + @"
      AND (@estado IS NULL OR UPPER(COALESCE(i.estado, '')) = UPPER(@estado))
      AND " + filtroCompania + @"
      AND (@codigoSolicitud IS NULL OR i.codigo_solicitud = @codigoSolicitud)
),
nc AS (
    SELECT h.codigo_inspeccion,
           COUNT(*) FILTER (WHERE UPPER(COALESCE(h.estado, 'ABIERTO')) <> 'CERRADO') AS nc_abiertas
    FROM aocr_tbhallazgo h
    GROUP BY h.codigo_inspeccion
)
SELECT
    COUNT(*) AS asignadas,
    COUNT(*) FILTER (WHERE UPPER(base.estado) IN ('SOLICITUD_INSPECCION_CREADA','VERIFICACION_SOLICITUD','ACEPTADA','SUBSANADA','EN_INSPECCION','INFORME_ELABORADO')) AS pendientes,
    COUNT(*) FILTER (WHERE COALESCE(nc.nc_abiertas,0) > 0) AS con_nc,
    COUNT(*) FILTER (WHERE UPPER(base.estado) IN ('CERRADA','CANCELADA')) AS cerradas,
    COUNT(*) FILTER (WHERE UPPER(base.estado) IN ('OBSERVADA','RESULTADO_NO_SATISFACTORIO','OBSERVACION_DOCUMENTAL')) AS requieren_nueva,
    AVG(EXTRACT(EPOCH FROM (COALESCE(base.updated_at, NOW()) - COALESCE(base.created_at, COALESCE(base.updated_at, NOW())))) / 3600.0) AS promedio_horas
FROM base
LEFT JOIN nc ON nc.codigo_inspeccion = base.codigo_inspeccion;";

                    using (var cmd = new NpgsqlCommand(sqlResumen, cn))
                    {
                        cmd.Parameters.AddWithValue("@codigoInspector", codigoInspector);
                        cmd.Parameters.AddWithValue("@fechaDesde", (object)fechaDesde ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fechaHasta", (object)fechaHasta ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@estado", string.IsNullOrWhiteSpace(estado) ? (object)DBNull.Value : estado.Trim());
                        cmd.Parameters.AddWithValue("@compania", string.IsNullOrWhiteSpace(compania) ? (object)DBNull.Value : "%" + compania.Trim() + "%");
                        cmd.Parameters.AddWithValue("@codigoSolicitud", (object)codigoSolicitud ?? DBNull.Value);

                        using (var rd = cmd.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                vm.InspeccionesAsignadas = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                                vm.InspeccionesPendientes = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                                vm.InspeccionesConNc = rd.IsDBNull(2) ? 0 : rd.GetInt32(2);
                                vm.InspeccionesCerradas = rd.IsDBNull(3) ? 0 : rd.GetInt32(3);
                                vm.InspeccionesRequierenNueva = rd.IsDBNull(4) ? 0 : rd.GetInt32(4);
                                vm.TiempoPromedioAtencionHoras = rd.IsDBNull(5) ? 0m : Convert.ToDecimal(rd.GetDouble(5));
                            }
                        }
                    }

                    var sqlUltimas = @"
SELECT i.codigo_inspeccion,
       i.codigo_solicitud,
       " + numeroInspeccionExpr + @" AS numero_inspeccion,
       COALESCE(i.estado, '') AS estado,
       COALESCE(i.resultado, '') AS resultado,
       " + operadorExpr + @" AS operador,
       " + fechaSelectExpr + @" AS fecha_programada,
       " + updatedAtExpr + @" AS updated_at,
       EXISTS (
           SELECT 1 FROM aocr_tbhallazgo h
           WHERE h.codigo_inspeccion = i.codigo_inspeccion
             AND UPPER(COALESCE(h.estado,'ABIERTO')) <> 'CERRADO'
       ) AS tiene_nc
FROM aocr_tbinspeccion i
LEFT JOIN aocr_tbsolicitud s ON s.codigo_solicitud = i.codigo_solicitud
WHERE i.codigo_inspector = @codigoInspector
  AND " + filtroFechaDesde + @"
  AND " + filtroFechaHasta + @"
  AND (@estado IS NULL OR UPPER(COALESCE(i.estado, '')) = UPPER(@estado))
  AND " + filtroCompania + @"
  AND (@codigoSolicitud IS NULL OR i.codigo_solicitud = @codigoSolicitud)
ORDER BY COALESCE(" + updatedAtExpr + @", " + createdAtExpr + @", NOW()) DESC
LIMIT @limite;";

                    using (var cmd = new NpgsqlCommand(sqlUltimas, cn))
                    {
                        cmd.Parameters.AddWithValue("@codigoInspector", codigoInspector);
                        cmd.Parameters.AddWithValue("@fechaDesde", (object)fechaDesde ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fechaHasta", (object)fechaHasta ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@estado", string.IsNullOrWhiteSpace(estado) ? (object)DBNull.Value : estado.Trim());
                        cmd.Parameters.AddWithValue("@compania", string.IsNullOrWhiteSpace(compania) ? (object)DBNull.Value : "%" + compania.Trim() + "%");
                        cmd.Parameters.AddWithValue("@codigoSolicitud", (object)codigoSolicitud ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@limite", 10);

                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                vm.UltimasInspecciones.Add(new InspectorInspeccionItemViewModel
                                {
                                    CodigoInspeccion = rd.IsDBNull(0) ? 0 : rd.GetInt32(0),
                                    CodigoSolicitud = rd.IsDBNull(1) ? 0 : rd.GetInt32(1),
                                    NumeroInspeccion = rd.IsDBNull(2) ? string.Empty : rd.GetString(2),
                                    Estado = rd.IsDBNull(3) ? string.Empty : rd.GetString(3),
                                    Resultado = rd.IsDBNull(4) ? string.Empty : rd.GetString(4),
                                    Operador = rd.IsDBNull(5) ? string.Empty : rd.GetString(5),
                                    FechaProgramada = rd.IsDBNull(6) ? (DateTime?)null : rd.GetDateTime(6),
                                    UltimaActualizacion = rd.IsDBNull(7) ? (DateTime?)null : rd.GetDateTime(7),
                                    TieneNoConformidadAbierta = !rd.IsDBNull(8) && rd.GetBoolean(8)
                                });
                            }
                        }
                    }

                    var sqlAlertas = @"
SELECT i.codigo_inspeccion,
       i.codigo_solicitud,
       COALESCE(i.estado, '') AS estado,
       COALESCE(" + updatedAtExpr + @", " + createdAtExpr + @", NOW()) AS fecha
FROM aocr_tbinspeccion i
WHERE i.codigo_inspector = @codigoInspector
  AND UPPER(COALESCE(i.estado, '')) IN ('OBSERVADA','RESULTADO_NO_SATISFACTORIO','OBSERVACION_DOCUMENTAL','VIATICOS_REQUERIDOS')
ORDER BY COALESCE(" + updatedAtExpr + @", " + createdAtExpr + @", NOW()) DESC
LIMIT @limite;";

                    using (var cmd = new NpgsqlCommand(sqlAlertas, cn))
                    {
                        cmd.Parameters.AddWithValue("@codigoInspector", codigoInspector);
                        cmd.Parameters.AddWithValue("@limite", 8);

                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                var codigoInspeccion = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                                var codigoSolicitudDb = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                                var estadoDb = rd.IsDBNull(2) ? string.Empty : rd.GetString(2);
                                var fechaDb = rd.IsDBNull(3) ? DateTime.Now : rd.GetDateTime(3);

                                vm.AlertasUrgentes.Add(new InspectorAlertaViewModel
                                {
                                    Tipo = "INSPECCION",
                                    Titulo = "Acción urgente de inspección",
                                    Mensaje = "Inspección " + codigoInspeccion + " en estado " + estadoDb + ". Requiere atención.",
                                    UrlDestino = "/Inspeccion/Detalle/" + codigoInspeccion,
                                    Severidad = "ALTA",
                                    Fecha = fechaDb
                                });

                                if (codigoSolicitudDb > 0)
                                {
                                    vm.AlertasUrgentes.Add(new InspectorAlertaViewModel
                                    {
                                        Tipo = "SOLICITUD",
                                        Titulo = "Solicitud asociada requiere seguimiento",
                                        Mensaje = "Solicitud " + codigoSolicitudDb + " asociada a inspección " + codigoInspeccion + ".",
                                        UrlDestino = "/SolicitudAOCR/Detalle/" + codigoSolicitudDb,
                                        Severidad = "MEDIA",
                                        Fecha = fechaDb
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("InspectorDashboardService.ObtenerDashboard error. Inspector={0}. Error={1}", codigoInspector, ex);
            }

            if (vm.AlertasUrgentes.Count > 8)
            {
                vm.AlertasUrgentes = vm.AlertasUrgentes.GetRange(0, 8);
            }

            return vm;
        }

        private bool ExisteColumna(NpgsqlConnection cn, string tabla, string columna)
        {
            const string sql = @"
SELECT COUNT(1)
FROM information_schema.columns
WHERE table_name = @tabla
  AND column_name = @columna
  AND table_schema = ANY (current_schemas(false));";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                cmd.Parameters.AddWithValue("@columna", columna);
                var result = cmd.ExecuteScalar();
                return result != null && Convert.ToInt32(result) > 0;
            }
        }
    }
}
