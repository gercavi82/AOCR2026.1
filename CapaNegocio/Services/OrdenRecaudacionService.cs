using System;
using System.Globalization;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Services;
using Npgsql;

namespace CapaNegocio.Services
{
    public class OrdenRecaudacionService
    {
        private readonly string _connectionString;
        private readonly ILoggingService _logger;
        private readonly OrdenRecaudacionDAO _ordenDao;

        public OrdenRecaudacionService()
            : this(
                new SecureConfigurationService().GetConnectionString("PostgreSQL")
                ?? new SecureConfigurationService().GetConnectionString("AOCRConnection")
                ?? string.Empty,
                new OrdenRecaudacionDAO())
        {
        }

        public OrdenRecaudacionService(string connectionString, OrdenRecaudacionDAO ordenDao = null)
        {
            _connectionString = connectionString ?? string.Empty;
            _ordenDao = ordenDao ?? new OrdenRecaudacionDAO();
            _logger = LoggingServiceFactory.Create();
        }

        public bool PuedeRtContinuarFlujoAocr(int codigoSolicitud, int codigoUsuario = 0)
        {
            var estadoOrden = codigoUsuario > 0
                ? _ordenDao.ObtenerUltimoEstadoOrdenPorSolicitudOUsuario(codigoSolicitud, codigoUsuario)
                : ObtenerUltimoEstadoOrdenPorSolicitud(codigoSolicitud);
            if (string.IsNullOrWhiteSpace(estadoOrden))
            {
                return false;
            }

            var estadoNormalizado = EstadoOrden.NormalizarEstado(estadoOrden);
            return !string.Equals(estadoNormalizado, EstadoOrden.Borrador, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(estadoNormalizado, EstadoOrden.Anulada, StringComparison.OrdinalIgnoreCase);
        }

        public string GenerarNumeroOrdenInstitucional(int anio)
        {
            if (anio <= 0)
            {
                anio = DateTime.Now.Year;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("No existe una conexión PostgreSQL configurada para generar números de orden.");
            }

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

                const string sql = @"
                    SELECT COALESCE(
                        MAX(CAST(regexp_replace(numero_orden, '.*AOCR', '') AS INTEGER)),
                        0
                    ) + 1
                    FROM public.aocr_or_orden
                    WHERE numero_orden ~ ('^DGAC-OR-' || @anio::text || '-AOCR[0-9]+$');";

                var siguiente = 1;
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@anio", anio);
                    var scalar = cmd.ExecuteScalar();
                    if (scalar != null && scalar != DBNull.Value)
                    {
                        siguiente = Convert.ToInt32(scalar);
                    }
                }

                for (var intentos = 0; intentos < 20; intentos++)
                {
                    var numeroOrden = string.Format(CultureInfo.InvariantCulture, "DGAC-OR-{0}-AOCR{1}", anio, siguiente.ToString("D3", CultureInfo.InvariantCulture));
                    if (!_ordenDao.ExisteNumeroOrden(numeroOrden))
                    {
                        return numeroOrden;
                    }

                    siguiente++;
                }
            }

            _logger.LogWarning("OrdenRecaudacionService.GenerarNumeroOrdenInstitucional: se agotaron los intentos para generar un número único.");
            return string.Format(CultureInfo.InvariantCulture, "DGAC-OR-{0}-AOCR{1}", anio, (DateTime.Now.Ticks % 1000L).ToString("D3", CultureInfo.InvariantCulture));
        }

        public string GenerarNumeroOrdenAocr(int anio)
        {
            return GenerarNumeroOrdenInstitucional(anio);
        }

        private string ObtenerUltimoEstadoOrdenPorSolicitud(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0 || string.IsNullOrWhiteSpace(_connectionString))
            {
                return string.Empty;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    const string sql = @"
                        SELECT COALESCE(o.estado, '')
                        FROM public.aocr_or_orden o
                        WHERE TRIM(COALESCE(o.codigo_solicitud::text, '')) = @codigo_solicitud_text
                        ORDER BY o.fecha_creacion DESC NULLS LAST, o.id DESC
                        LIMIT 1;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigo_solicitud_text", codigoSolicitud.ToString(CultureInfo.InvariantCulture));
                        var scalar = cmd.ExecuteScalar();
                        return scalar == null || scalar == DBNull.Value ? string.Empty : scalar.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return string.Empty;
            }
        }
    }
}