using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Npgsql;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaNegocio.Services;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Servicio empresarial optimizado para Dashboard de Órdenes de Recaudación
    /// Implementa queries eficientes y cálculos empresariales
    /// </summary>
    public class DashboardOrdenesService
    {
        private readonly string _connectionString;
        private readonly OrdenRecaudacionDAO _dao;

        public DashboardOrdenesService()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString
                ?? System.Configuration.ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
            _dao = new OrdenRecaudacionDAO();
        }

        public DashboardOrdenesService(string connectionString)
        {
            _connectionString = connectionString;
            _dao = new OrdenRecaudacionDAO(_connectionString);
        }

        #region KPIs Empresariales

        /// <summary>
        /// DTO para KPIs del Dashboard
        /// </summary>
        public class DashboardKPIs
        {
            public int TotalOrdenes { get; set; }
            public int OrdenesPendientes { get; set; }
            public int OrdenesCompletadas { get; set; }
            public int OrdenesAnuladas { get; set; }
            public int OrdenesRechazadas { get; set; }
            public decimal MontoTotal { get; set; }
            public decimal MontoPendiente { get; set; }
            public decimal MontoCompletado { get; set; }
            public string UltimaOrden { get; set; }
            public DateTime? FechaUltimaOrden { get; set; }
            public decimal PromedioMonto { get; set; }
            public int OrdenesDelMes { get; set; }
            public decimal MontoDelMes { get; set; }
            public double TasaCompletamiento { get; set; }
        }

        /// <summary>
        /// Obtiene KPIs optimizados con una sola consulta SQL
        /// </summary>
        public DashboardKPIs ObtenerKPIs(int? userId = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
        {
            var kpis = new DashboardKPIs();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // Query optimizado que calcula todos los KPIs en una sola consulta
                    var sql = @"
                        WITH estadisticas AS (
                            SELECT 
                                COUNT(*) as total_ordenes,
                                SUM(CASE 
                                    WHEN UPPER(estado) IN ('BORRADOR', 'GENERADA', 'ENVIADA', 'APROBADA') 
                                    THEN 1 ELSE 0 
                                END) as pendientes,
                                SUM(CASE 
                                    WHEN UPPER(estado) IN ('PAGADA', 'FACTURADA') 
                                    THEN 1 ELSE 0 
                                END) as completadas,
                                SUM(CASE 
                                    WHEN UPPER(estado) = 'ANULADA' 
                                    THEN 1 ELSE 0 
                                END) as anuladas,
                                SUM(CASE 
                                    WHEN UPPER(estado) = 'RECHAZADA' 
                                    THEN 1 ELSE 0 
                                END) as rechazadas,
                                COALESCE(SUM(total), 0) as monto_total,
                                COALESCE(SUM(CASE 
                                    WHEN UPPER(estado) IN ('BORRADOR', 'GENERADA', 'ENVIADA', 'APROBADA') 
                                    THEN total ELSE 0 
                                END), 0) as monto_pendiente,
                                COALESCE(SUM(CASE 
                                    WHEN UPPER(estado) IN ('PAGADA', 'FACTURADA') 
                                    THEN total ELSE 0 
                                END), 0) as monto_completado,
                                COUNT(CASE 
                                    WHEN fecha_creacion >= DATE_TRUNC('month', CURRENT_DATE)
                                    THEN 1 
                                END) as ordenes_del_mes,
                                COALESCE(SUM(CASE 
                                    WHEN fecha_creacion >= DATE_TRUNC('month', CURRENT_DATE)
                                    THEN total ELSE 0 
                                END), 0) as monto_del_mes
                            FROM aocr_or_orden 
                            WHERE 1=1
                                " + (userId.HasValue ? " AND codigo_usuario = @userId" : "") + @"
                                " + (fechaDesde.HasValue ? " AND fecha_creacion >= @fechaDesde" : "") + @"
                                " + (fechaHasta.HasValue ? " AND fecha_creacion <= @fechaHasta" : "") + @"
                        ),
                        ultima_orden AS (
                            SELECT numero_orden, fecha_creacion
                            FROM aocr_or_orden 
                            WHERE 1=1
                                " + (userId.HasValue ? " AND codigo_usuario = @userId" : "") + @"
                                " + (fechaDesde.HasValue ? " AND fecha_creacion >= @fechaDesde" : "") + @"
                                " + (fechaHasta.HasValue ? " AND fecha_creacion <= @fechaHasta" : "") + @"
                            ORDER BY fecha_creacion DESC 
                            LIMIT 1
                        )
                        SELECT 
                            e.*,
                            u.numero_orden as ultima_orden_numero,
                            u.fecha_creacion as ultima_orden_fecha
                        FROM estadisticas e
                        LEFT JOIN ultima_orden u ON true";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        if (userId.HasValue)
                            cmd.Parameters.AddWithValue("@userId", userId.Value);
                        if (fechaDesde.HasValue)
                            cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde.Value);
                        if (fechaHasta.HasValue)
                            cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta.Value);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                kpis.TotalOrdenes = Convert.ToInt32(reader["total_ordenes"]);
                                kpis.OrdenesPendientes = Convert.ToInt32(reader["pendientes"]);
                                kpis.OrdenesCompletadas = Convert.ToInt32(reader["completadas"]);
                                kpis.OrdenesAnuladas = Convert.ToInt32(reader["anuladas"]);
                                kpis.OrdenesRechazadas = Convert.ToInt32(reader["rechazadas"]);
                                kpis.MontoTotal = Convert.ToDecimal(reader["monto_total"]);
                                kpis.MontoPendiente = Convert.ToDecimal(reader["monto_pendiente"]);
                                kpis.MontoCompletado = Convert.ToDecimal(reader["monto_completado"]);
                                kpis.OrdenesDelMes = Convert.ToInt32(reader["ordenes_del_mes"]);
                                kpis.MontoDelMes = Convert.ToDecimal(reader["monto_del_mes"]);
                                
                                kpis.UltimaOrden = reader["ultima_orden_numero"]?.ToString() ?? "N/A";
                                kpis.FechaUltimaOrden = reader["ultima_orden_fecha"] != DBNull.Value 
                                    ? Convert.ToDateTime(reader["ultima_orden_fecha"]) 
                                    : (DateTime?)null;

                                // Cálculos derivados
                                kpis.PromedioMonto = kpis.TotalOrdenes > 0 
                                    ? kpis.MontoTotal / kpis.TotalOrdenes 
                                    : 0;
                                
                                kpis.TasaCompletamiento = kpis.TotalOrdenes > 0 
                                    ? Math.Round((double)kpis.OrdenesCompletadas / kpis.TotalOrdenes * 100, 2)
                                    : 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error - en producción usar ILogger
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerKPIs: {ex.Message}");
                
                // Retornar KPIs vacíos en caso de error
                kpis = new DashboardKPIs
                {
                    UltimaOrden = "Error",
                    TasaCompletamiento = 0
                };
            }

            return kpis;
        }

        #endregion

        #region Datos para Grillas

        /// <summary>
        /// DTO optimizado para grilla del Dashboard
        /// </summary>
        public class OrdenDashboardDTO
        {
            public int Id { get; set; }
            public string NumeroOrden { get; set; }
            public DateTime FechaCreacion { get; set; }
            public string Estado { get; set; }
            public string EstadoColor { get; set; }
            public string NombreContribuyente { get; set; }
            public decimal Total { get; set; }
            public string Usuario { get; set; }
            public DateTime? FechaUltimaModificacion { get; set; }
            public bool PuedeEditar { get; set; }
            public bool PuedeCambiarEstado { get; set; }
            public List<string> AccionesPermitidas { get; set; }
        }

        /// <summary>
        /// Obtiene órdenes para la grilla con filtros optimizados
        /// </summary>
        public List<OrdenDashboardDTO> ObtenerOrdenesParaDashboard(
            int? userId = null,
            string estado = null,
            DateTime? fechaDesde = null, 
            DateTime? fechaHasta = null,
            string numeroOrden = null,
            List<string> rolesUsuario = null,
            int limite = 100)
        {
            var ordenes = new List<OrdenDashboardDTO>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    var sql = @"
                        SELECT 
                            o.id,
                            o.numero_orden,
                            o.fecha_creacion,
                            o.estado,
                            o.nombre_contribuyente,
                            o.compania,
                            o.total,
                            o.codigo_usuario,
                            u.nombre as usuario_nombre,
                            o.fecha_modificacion
                        FROM aocr_or_orden o
                        LEFT JOIN aocr_tbusuario u ON o.codigo_usuario = u.codigousuario
                        WHERE 1=1";

                    var parametros = new List<NpgsqlParameter>();

                    if (userId.HasValue)
                    {
                        sql += " AND o.codigo_usuario = @userId";
                        parametros.Add(new NpgsqlParameter("@userId", userId.Value));
                    }

                    if (!string.IsNullOrWhiteSpace(estado))
                    {
                        sql += " AND UPPER(o.estado) = UPPER(@estado)";
                        parametros.Add(new NpgsqlParameter("@estado", estado.Trim()));
                    }

                    if (fechaDesde.HasValue)
                    {
                        sql += " AND o.fecha_creacion >= @fechaDesde";
                        parametros.Add(new NpgsqlParameter("@fechaDesde", fechaDesde.Value));
                    }

                    if (fechaHasta.HasValue)
                    {
                        sql += " AND o.fecha_creacion <= @fechaHasta";
                        parametros.Add(new NpgsqlParameter("@fechaHasta", fechaHasta.Value));
                    }

                    if (!string.IsNullOrWhiteSpace(numeroOrden))
                    {
                        sql += " AND UPPER(o.numero_orden) LIKE UPPER(@numeroOrden)";
                        parametros.Add(new NpgsqlParameter("@numeroOrden", $"%{numeroOrden.Trim()}%"));
                    }

                    sql += " ORDER BY o.fecha_creacion DESC";
                    
                    if (limite > 0)
                    {
                        sql += $" LIMIT {limite}";
                    }

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        foreach (var param in parametros)
                            cmd.Parameters.Add(param);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var estado_orden = reader["estado"]?.ToString()?.Trim() ?? "";
                                var es_propietario = userId.HasValue && Convert.ToInt32(reader["codigo_usuario"]) == userId.Value;

                                var orden = new OrdenDashboardDTO
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    NumeroOrden = reader["numero_orden"]?.ToString() ?? "",
                                    FechaCreacion = Convert.ToDateTime(reader["fecha_creacion"]),
                                    Estado = estado_orden,
                                    EstadoColor = EstadoOrdenService.ObtenerColorEstado(estado_orden),
                                    NombreContribuyente = reader["nombre_contribuyente"]?.ToString() ?? 
                                                         reader["compania"]?.ToString() ?? "Sin especificar",
                                    Total = reader["total"] != DBNull.Value ? Convert.ToDecimal(reader["total"]) : 0,
                                    Usuario = reader["usuario_nombre"]?.ToString() ?? "N/D",
                                    FechaUltimaModificacion = reader["fecha_modificacion"] != DBNull.Value 
                                        ? Convert.ToDateTime(reader["fecha_modificacion"]) 
                                        : (DateTime?)null
                                };

                                // Calcular permisos usando el servicio de estados
                                if (rolesUsuario != null)
                                {
                                    orden.PuedeEditar = EstadoOrdenService.PuedeEditar(estado_orden, rolesUsuario) && es_propietario;
                                    orden.AccionesPermitidas = EstadoOrdenService.ObtenerTransicionesPermitidas(estado_orden, rolesUsuario);
                                    orden.PuedeCambiarEstado = orden.AccionesPermitidas.Any();
                                }

                                ordenes.Add(orden);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error - en producción usar ILogger
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerOrdenesParaDashboard: {ex.Message}");
            }

            return ordenes;
        }

        #endregion

        #region Métricas de Rendimiento del Mes

        /// <summary>
        /// DTO para métricas mensuales
        /// </summary>
        public class MetricasMensuales
        {
            public string Mes { get; set; }
            public int Ordenes { get; set; }
            public decimal Monto { get; set; }
            public int Completadas { get; set; }
            public double TasaExito { get; set; }
        }

        /// <summary>
        /// Obtiene métricas de los últimos 12 meses para gráficos
        /// </summary>
        public List<MetricasMensuales> ObtenerMetricasMensuales(int? userId = null)
        {
            var metricas = new List<MetricasMensuales>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    var sql = @"
                        SELECT 
                            TO_CHAR(DATE_TRUNC('month', fecha_creacion), 'YYYY-MM') as mes,
                            COUNT(*) as ordenes,
                            COALESCE(SUM(total), 0) as monto,
                            SUM(CASE 
                                WHEN UPPER(estado) IN ('PAGADA', 'FACTURADA') 
                                THEN 1 ELSE 0 
                            END) as completadas
                        FROM aocr_or_orden 
                        WHERE fecha_creacion >= DATE_TRUNC('month', CURRENT_DATE) - INTERVAL '11 months'
                            " + (userId.HasValue ? " AND codigo_usuario = @userId" : "") + @"
                        GROUP BY DATE_TRUNC('month', fecha_creacion)
                        ORDER BY DATE_TRUNC('month', fecha_creacion) DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        if (userId.HasValue)
                            cmd.Parameters.AddWithValue("@userId", userId.Value);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var ordenes = Convert.ToInt32(reader["ordenes"]);
                                var completadas = Convert.ToInt32(reader["completadas"]);

                                metricas.Add(new MetricasMensuales
                                {
                                    Mes = reader["mes"].ToString(),
                                    Ordenes = ordenes,
                                    Monto = Convert.ToDecimal(reader["monto"]),
                                    Completadas = completadas,
                                    TasaExito = ordenes > 0 ? Math.Round((double)completadas / ordenes * 100, 2) : 0
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerMetricasMensuales: {ex.Message}");
            }

            return metricas;
        }

        #endregion

        #region Acciones Rápidas

        /// <summary>
        /// Ejecuta una acción rápida en una orden (cambio de estado)
        /// </summary>
        public bool EjecutarAccionRapida(int ordenId, string accion, int userId, List<string> rolesUsuario, string observacion = null)
        {
            try
            {
                var orden = _dao.ObtenerPorId(ordenId);
                if (orden == null)
                    return false;

                // Validar permisos
                if (!EstadoOrdenService.TienePermisosParaTransicion(orden.Estado, accion, rolesUsuario))
                    return false;

                // Validar reglas de negocio
                if (!EstadoOrdenService.ValidarReglasNegocio(orden.Estado, accion, orden.Total ?? 0, true, out var mensaje))
                    return false;

                // Si es RECHAZAR, requiere observación
                if (accion == EstadoOrdenService.Estados.RECHAZADA && string.IsNullOrWhiteSpace(observacion))
                    return false;

                // Ejecutar cambio de estado
                return _dao.CambiarEstado(ordenId, accion, observacion);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en EjecutarAccionRapida: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}