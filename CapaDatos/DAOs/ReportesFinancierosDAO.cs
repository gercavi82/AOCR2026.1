using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CapaModelo.ReportesFinancieros;
using Dapper;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class ReportesFinancierosDAO
    {
        private readonly string _connectionString;

        private static readonly string[] EstadosPagados = { "PAGADA", "FACTURADA", "COMPLETADA", "VALIDADO" };
        private static readonly string[] EstadosAnulados = { "ANULADA", "RECHAZADA" };

        private const string BaseFromSql = @"
FROM aocr_or_orden o
LEFT JOIN aocr_tbsolicitud s ON s.codigo_solicitud::text = o.codigo_solicitud::text
LEFT JOIN usuario u ON u.idusuario = o.codigo_usuario
LEFT JOIN LATERAL (
    SELECT
        p.codigo_pago,
        p.codigo_solicitud,
        p.monto,
        p.estado,
        p.fecha_pago,
        p.observaciones,
        p.validado_por
    FROM aocr_tbpago p
    WHERE p.codigo_solicitud::text = o.id::text
       OR p.codigo_solicitud::text = o.codigo_solicitud::text
    ORDER BY p.fecha_pago DESC NULLS LAST, p.codigo_pago DESC
    LIMIT 1
) p ON TRUE";

        public ReportesFinancierosDAO()
        {
            _connectionString = ConexionDAO.ObtenerCadenaConexion();
        }

        public ReportesFinancierosDAO(string connectionString)
        {
            _connectionString = connectionString;
        }

        public ReporteResumenDTO ObtenerResumen(FiltroReporteDTO filtro)
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                var parametros = CrearParametrosBase();
                var filtros = ConstruirCondicionesFiltro(filtro, parametros, false);
                var where = ConstruirWhere(filtros);

                var sql = $@"
SELECT
    COUNT(*) AS TotalOrdenesGeneradas,
    COUNT(CASE WHEN UPPER(o.estado) = ANY(@EstadosPagados) THEN 1 END) AS TotalOrdenesPagadas,
    COUNT(CASE
        WHEN NOT (UPPER(o.estado) = ANY(@EstadosPagados) OR UPPER(o.estado) = ANY(@EstadosAnulados))
        THEN 1 END) AS TotalPendientes,
    COUNT(CASE WHEN UPPER(o.estado) = ANY(@EstadosAnulados) THEN 1 END) AS TotalAnuladas,
    COALESCE(SUM(CASE WHEN UPPER(o.estado) = ANY(@EstadosPagados) THEN o.total ELSE 0 END), 0) AS TotalRecaudado,
    COALESCE(SUM(CASE
        WHEN NOT (UPPER(o.estado) = ANY(@EstadosPagados) OR UPPER(o.estado) = ANY(@EstadosAnulados))
        THEN o.total ELSE 0 END), 0) AS TotalPendientePorCobrar,
    COALESCE(SUM(o.total), 0) AS TotalFiltrado,
    COALESCE(SUM(o.subtotal), 0) AS SubtotalFiltrado,
    COALESCE(SUM(o.admin), 0) AS AdministracionFiltrada
{BaseFromSql}
{where};";

                var resumen = db.QueryFirstOrDefault<ReporteResumenDTO>(sql, parametros) ?? new ReporteResumenDTO();
                resumen.IngresosPorMes = ObtenerIngresosPorMes(filtro);
                resumen.TotalesPorEstado = ObtenerTotalesPorEstado(filtro);
                resumen.RecaudacionPorTramite = ObtenerRecaudacionPorTramite(filtro);
                resumen.RecaudacionPorUnidad = ObtenerRecaudacionPorUnidad(filtro);
                resumen.Anulaciones = ObtenerAnulaciones(filtro);

                return resumen;
            }
        }

        public IList<ReporteOrdenDTO> ObtenerOrdenes(FiltroReporteDTO filtro)
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                var parametros = CrearParametrosBase();
                var filtros = ConstruirCondicionesFiltro(filtro, parametros, false);
                var where = ConstruirWhere(filtros);

                var sql = $@"
SELECT
    o.id AS OrdenId,
    o.numero_orden AS NumeroOrden,
    o.fecha_creacion AS FechaCreacion,
    p.fecha_pago AS FechaPago,
    COALESCE(o.estado, 'N/D') AS Estado,
    COALESCE(o.codigo_usuario, 0) AS UsuarioSolicitanteId,
    COALESCE(
        NULLIF(TRIM(COALESCE(u.nombreusuario, '') || ' ' || COALESCE(u.apellidousuario, '')), ''),
        NULLIF(TRIM(u.codigousuario), ''),
        COALESCE(o.compania, 'N/D')
    ) AS UsuarioSolicitante,
    s.tipo_solicitud AS TipoTramiteId,
    COALESCE('Tramite ' || s.tipo_solicitud::text, 'N/D') AS TipoTramite,
    COALESCE(p.validado_por, '') AS RolGestion,
    COALESCE(NULLIF(o.lugar_emision, ''), 'Sin unidad') AS Unidad,
    COALESCE(o.compania, 'N/D') AS Compania,
    COALESCE(o.ruc_cedula, 'N/D') AS RucCedula,
    COALESCE(o.subtotal, 0) AS Subtotal,
    COALESCE(o.admin, 0) AS Administracion,
    COALESCE(o.total, 0) AS Total,
    CASE WHEN UPPER(o.estado) = ANY(@EstadosPagados) THEN COALESCE(o.total, 0) ELSE 0 END AS MontoPagado,
    CASE
        WHEN UPPER(o.estado) = ANY(@EstadosPagados) OR UPPER(o.estado) = ANY(@EstadosAnulados) THEN 0
        ELSE COALESCE(o.total, 0)
    END AS SaldoPendiente,
    COALESCE(NULLIF(o.observacion, ''), NULLIF(p.observaciones, ''), '') AS Observacion,
    CASE
        WHEN UPPER(o.estado) = ANY(@EstadosAnulados)
        THEN COALESCE(NULLIF(o.observacion, ''), NULLIF(p.observaciones, ''), 'Sin motivo')
        ELSE ''
    END AS MotivoAnulacion
{BaseFromSql}
{where}
ORDER BY o.fecha_creacion DESC, o.id DESC;";

                return db.Query<ReporteOrdenDTO>(sql, parametros).ToList();
            }
        }

        public IList<FiltroOpcionDTO> ObtenerUsuariosSolicitantes()
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                const string sql = @"
SELECT DISTINCT
    o.codigo_usuario::text AS Value,
    COALESCE(
        NULLIF(TRIM(COALESCE(u.nombreusuario, '') || ' ' || COALESCE(u.apellidousuario, '')), ''),
        NULLIF(TRIM(u.codigousuario), ''),
        'Usuario ' || o.codigo_usuario::text
    ) AS Text
FROM aocr_or_orden o
LEFT JOIN usuario u ON u.idusuario = o.codigo_usuario
WHERE o.codigo_usuario IS NOT NULL
ORDER BY Text;";

                return db.Query<FiltroOpcionDTO>(sql).ToList();
            }
        }

        public IList<FiltroOpcionDTO> ObtenerTiposTramite()
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                const string sql = @"
SELECT DISTINCT
    s.tipo_solicitud::text AS Value,
    'Tramite ' || s.tipo_solicitud::text AS Text
FROM aocr_tbsolicitud s
WHERE s.tipo_solicitud IS NOT NULL
ORDER BY s.tipo_solicitud;";

                return db.Query<FiltroOpcionDTO>(sql).ToList();
            }
        }

        public IList<FiltroOpcionDTO> ObtenerRolesGestion()
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                const string sql = @"
SELECT DISTINCT
    p.validado_por AS Value,
    p.validado_por AS Text
FROM aocr_tbpago p
WHERE p.validado_por IS NOT NULL
  AND TRIM(p.validado_por) <> ''
ORDER BY p.validado_por;";

                return db.Query<FiltroOpcionDTO>(sql).ToList();
            }
        }

        public IList<FiltroOpcionDTO> ObtenerUnidades()
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                const string sql = @"
SELECT DISTINCT
    TRIM(o.lugar_emision) AS Value,
    TRIM(o.lugar_emision) AS Text
FROM aocr_or_orden o
WHERE o.lugar_emision IS NOT NULL
  AND TRIM(o.lugar_emision) <> ''
ORDER BY TRIM(o.lugar_emision);";

                return db.Query<FiltroOpcionDTO>(sql).ToList();
            }
        }

        private IList<SerieMensualDTO> ObtenerIngresosPorMes(FiltroReporteDTO filtro)
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                var parametros = CrearParametrosBase();
                var filtros = ConstruirCondicionesFiltro(filtro, parametros, true);
                filtros.Add("UPPER(o.estado) = ANY(@EstadosPagados)");
                var where = ConstruirWhere(filtros);

                var sql = $@"
SELECT
    TO_CHAR(DATE_TRUNC('month', COALESCE(p.fecha_pago, o.fecha_creacion)), 'YYYY-MM') AS Etiqueta,
    COALESCE(SUM(o.total), 0) AS Total
{BaseFromSql}
{where}
GROUP BY DATE_TRUNC('month', COALESCE(p.fecha_pago, o.fecha_creacion))
ORDER BY DATE_TRUNC('month', COALESCE(p.fecha_pago, o.fecha_creacion));";

                return db.Query<SerieMensualDTO>(sql, parametros).ToList();
            }
        }

        private IList<EstadoTotalDTO> ObtenerTotalesPorEstado(FiltroReporteDTO filtro)
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                var parametros = CrearParametrosBase();
                var filtros = ConstruirCondicionesFiltro(filtro, parametros, false);
                var where = ConstruirWhere(filtros);

                var sql = $@"
SELECT
    UPPER(COALESCE(o.estado, 'N/D')) AS Estado,
    COUNT(*) AS Cantidad,
    COALESCE(SUM(o.total), 0) AS Total
{BaseFromSql}
{where}
GROUP BY UPPER(COALESCE(o.estado, 'N/D'))
ORDER BY Cantidad DESC, Estado;";

                return db.Query<EstadoTotalDTO>(sql, parametros).ToList();
            }
        }

        private IList<RecaudacionPorTramiteDTO> ObtenerRecaudacionPorTramite(FiltroReporteDTO filtro)
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                var parametros = CrearParametrosBase();
                var filtros = ConstruirCondicionesFiltro(filtro, parametros, true);
                filtros.Add("UPPER(o.estado) = ANY(@EstadosPagados)");
                var where = ConstruirWhere(filtros);

                var sql = $@"
SELECT
    COALESCE('Tramite ' || s.tipo_solicitud::text, 'N/D') AS Tramite,
    COUNT(*) AS Cantidad,
    COALESCE(SUM(o.total), 0) AS Total
{BaseFromSql}
{where}
GROUP BY COALESCE('Tramite ' || s.tipo_solicitud::text, 'N/D')
ORDER BY Total DESC;";

                return db.Query<RecaudacionPorTramiteDTO>(sql, parametros).ToList();
            }
        }

        private IList<RecaudacionPorUnidadDTO> ObtenerRecaudacionPorUnidad(FiltroReporteDTO filtro)
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                var parametros = CrearParametrosBase();
                var filtros = ConstruirCondicionesFiltro(filtro, parametros, true);
                filtros.Add("UPPER(o.estado) = ANY(@EstadosPagados)");
                var where = ConstruirWhere(filtros);

                var sql = $@"
SELECT
    COALESCE(NULLIF(o.lugar_emision, ''), 'Sin unidad') AS Unidad,
    COUNT(*) AS Cantidad,
    COALESCE(SUM(o.total), 0) AS Total
{BaseFromSql}
{where}
GROUP BY COALESCE(NULLIF(o.lugar_emision, ''), 'Sin unidad')
ORDER BY Total DESC;";

                return db.Query<RecaudacionPorUnidadDTO>(sql, parametros).ToList();
            }
        }

        private IList<ReporteAnulacionDTO> ObtenerAnulaciones(FiltroReporteDTO filtro)
        {
            using (IDbConnection db = new NpgsqlConnection(_connectionString))
            {
                var parametros = CrearParametrosBase();
                var filtros = ConstruirCondicionesFiltro(filtro, parametros, true);
                filtros.Add("UPPER(o.estado) = ANY(@EstadosAnulados)");
                var where = ConstruirWhere(filtros);

                var sql = $@"
SELECT
    o.numero_orden AS NumeroOrden,
    COALESCE(p.fecha_pago, o.fecha_creacion) AS Fecha,
    COALESCE(NULLIF(o.lugar_emision, ''), 'Sin unidad') AS Unidad,
    COALESCE(NULLIF(o.observacion, ''), NULLIF(p.observaciones, ''), 'Sin motivo') AS Motivo,
    COALESCE(NULLIF(p.validado_por, ''), 'N/D') AS RolGestion,
    COALESCE(NULLIF(p.observaciones, ''), '') AS Observaciones
{BaseFromSql}
{where}
ORDER BY COALESCE(p.fecha_pago, o.fecha_creacion) DESC
LIMIT 150;";

                return db.Query<ReporteAnulacionDTO>(sql, parametros).ToList();
            }
        }

        private static DynamicParameters CrearParametrosBase()
        {
            var parametros = new DynamicParameters();
            parametros.Add("@EstadosPagados", EstadosPagados);
            parametros.Add("@EstadosAnulados", EstadosAnulados);
            return parametros;
        }

        private static List<string> ConstruirCondicionesFiltro(
            FiltroReporteDTO filtro,
            DynamicParameters parametros,
            bool usarFechaPago)
        {
            var condiciones = new List<string>();
            if (filtro == null)
            {
                return condiciones;
            }

            var campoFecha = usarFechaPago
                ? "COALESCE(p.fecha_pago, o.fecha_creacion)::date"
                : "o.fecha_creacion::date";

            if (filtro.FechaDesde.HasValue)
            {
                condiciones.Add(campoFecha + " >= @FechaDesde");
                parametros.Add("@FechaDesde", filtro.FechaDesde.Value.Date);
            }

            if (filtro.FechaHasta.HasValue)
            {
                condiciones.Add(campoFecha + " <= @FechaHasta");
                parametros.Add("@FechaHasta", filtro.FechaHasta.Value.Date);
            }

            if (!string.IsNullOrWhiteSpace(filtro.EstadoNormalizado))
            {
                condiciones.Add("UPPER(o.estado) = @Estado");
                parametros.Add("@Estado", filtro.EstadoNormalizado);
            }

            if (filtro.UsuarioSolicitanteId.HasValue)
            {
                condiciones.Add("o.codigo_usuario = @UsuarioSolicitanteId");
                parametros.Add("@UsuarioSolicitanteId", filtro.UsuarioSolicitanteId.Value);
            }

            if (filtro.TipoTramiteId.HasValue)
            {
                condiciones.Add("s.tipo_solicitud = @TipoTramiteId");
                parametros.Add("@TipoTramiteId", filtro.TipoTramiteId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filtro.RolGestion))
            {
                condiciones.Add("COALESCE(p.validado_por, '') ILIKE @RolGestion");
                parametros.Add("@RolGestion", "%" + filtro.RolGestion.Trim() + "%");
            }

            if (!string.IsNullOrWhiteSpace(filtro.Unidad))
            {
                condiciones.Add("COALESCE(o.lugar_emision, '') ILIKE @Unidad");
                parametros.Add("@Unidad", "%" + filtro.Unidad.Trim() + "%");
            }

            return condiciones;
        }

        private static string ConstruirWhere(IReadOnlyCollection<string> condiciones)
        {
            return condiciones.Count == 0
                ? string.Empty
                : "WHERE " + string.Join(" AND ", condiciones);
        }
    }
}
