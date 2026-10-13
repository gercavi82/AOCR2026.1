using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Services;
using Npgsql;

namespace CapaNegocio.Services
{
    public class OrdenRecaudacionService
    {
        public static string PrepararCorreccionFinanciera(IList<CapaDatos.Entidades.DetalleOrden> detalles,
            IEnumerable<CapaDatos.Models.ConceptoModel> conceptos, string observacion)
        {
            if (string.IsNullOrWhiteSpace(observacion) || observacion.Length > 2000)
                return "Indique la justificación de la corrección (máximo 2000 caracteres).";
            if (detalles == null || detalles.Count == 0 || detalles.Count > 200)
                return "La orden debe contener entre 1 y 200 conceptos.";
            var catalogo = conceptos.ToDictionary(c => c.Id);
            foreach (var d in detalles)
            {
                if (d == null || d.Id < 0 || !d.ConceptoId.HasValue || !catalogo.ContainsKey(d.ConceptoId.Value)
                    || d.Cantidad <= 0 || d.Cantidad > 10000 || d.ValorUnitario < 0 || d.ValorUnitario > 1000000
                    || d.PorcentajeAdmin < 0 || d.PorcentajeAdmin > 100)
                    return "Revise los conceptos, cantidades, valores unitarios y porcentajes administrativos.";
                var concepto = catalogo[d.ConceptoId.Value];
                if (d.Id == 0 && !concepto.Activo) return "No se pueden agregar conceptos inactivos.";
                d.ConceptoCodigo = concepto.Codigo;
                d.ConceptoNombre = concepto.Nombre;
                d.Subtotal = Math.Round(d.Cantidad * d.ValorUnitario, 2, MidpointRounding.AwayFromZero);
                d.Admin = Math.Round(d.Subtotal * d.PorcentajeAdmin / 100m, 2, MidpointRounding.AwayFromZero);
                d.TotalLinea = d.Subtotal + d.Admin;
            }
            if (detalles.Sum(d => d.TotalLinea) <= 0) return "El total de la orden corregida debe ser mayor que cero.";
            return null;
        }
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

            _logger.LogInfo(string.Format(CultureInfo.InvariantCulture, "[ORDEN][NUMERACION_IN] Anio={0};", anio));

            for (var intento = 1; intento <= 10; intento++)
            {
                try
                {
                    using (var conn = new NpgsqlConnection(_connectionString))
                    {
                        conn.Open();

                        using (var tx = conn.BeginTransaction())
                        {
                            const string sqlInit = @"
                                CREATE TABLE IF NOT EXISTS public.aocr_correlativo_orden (
                                    anio integer PRIMARY KEY,
                                    ultimo_numero integer NOT NULL,
                                    fecha_actualizacion timestamp without time zone DEFAULT now()
                                );
                                INSERT INTO public.aocr_correlativo_orden (anio, ultimo_numero)
                                VALUES (@anio, 0)
                                ON CONFLICT (anio) DO NOTHING;";

                            using (var cmdInit = new NpgsqlCommand(sqlInit, conn, tx))
                            {
                                cmdInit.Parameters.AddWithValue("@anio", anio);
                                cmdInit.ExecuteNonQuery();
                            }

                            const string sqlInc = @"
                                UPDATE public.aocr_correlativo_orden
                                SET ultimo_numero = GREATEST(
                                        ultimo_numero + 1,
                                        COALESCE((
                                            SELECT MAX(
                                                CASE 
                                                    WHEN numero_orden ~ '^DGAC-OR-\d{4}-AOCR\d+$' 
                                                    THEN NULLIF(SUBSTRING(numero_orden FROM 'AOCR(\d+)'), '')::integer 
                                                    ELSE 0 
                                                END
                                            ) + 1
                                            FROM public.aocr_or_orden
                                            WHERE numero_orden LIKE 'DGAC-OR-' || @anio || '%'
                                        ), 1)
                                    ),
                                    fecha_actualizacion = NOW()
                                WHERE anio = @anio
                                RETURNING ultimo_numero;";

                            int correlativo;
                            using (var cmdInc = new NpgsqlCommand(sqlInc, conn, tx))
                            {
                                cmdInc.Parameters.AddWithValue("@anio", anio);
                                var scalar = cmdInc.ExecuteScalar();
                                correlativo = Convert.ToInt32(scalar);
                            }

                            tx.Commit();

                            var numeroOrden = string.Format(
                                CultureInfo.InvariantCulture,
                                "DGAC-OR-{0}-AOCR{1}",
                                anio,
                                correlativo.ToString("D3", CultureInfo.InvariantCulture));

                            if (!_ordenDao.ExisteNumeroOrden(numeroOrden))
                            {
                                _logger.LogInfo(string.Format(
                                    CultureInfo.InvariantCulture,
                                    "[ORDEN][NUMERACION_GENERADA] Anio={0}; Correlativo={1}; NumeroOrden={2}; Intento={3}",
                                    anio,
                                    correlativo,
                                    numeroOrden,
                                    intento));

                                return numeroOrden;
                            }
                            else
                            {
                                _logger.LogWarning(string.Format(
                                    CultureInfo.InvariantCulture,
                                    "[ORDEN][NUMERACION_DUPLICADA_DENY] NumeroOrden={0}; Motivo=Existe en BD; Intento={1}",
                                    numeroOrden,
                                    intento));
                            }
                        }
                    }
                }
                catch (PostgresException pgEx) when (pgEx.SqlState == "23505")
                {
                    _logger.LogWarning(string.Format(
                        CultureInfo.InvariantCulture,
                        "[ORDEN][NUMERACION_CONCURRENCY_RETRY] Unique violation en intento {0}: {1}",
                        intento,
                        pgEx.Message));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex);
                }
            }

            throw new InvalidOperationException("No fue posible generar la orden en este momento. Intente nuevamente.");
        }

        public string GenerarNumeroOrdenAocr(int anio)
        {
            return GenerarNumeroOrdenInstitucional(anio);
        }

        public string GenerarNumeroOrdenAocr(int anio, int companiaId, int usuarioId, int? solicitudId)
        {
            _logger.LogInfo(string.Format(
                CultureInfo.InvariantCulture,
                "[ORDEN][NUMERACION_IN] UsuarioId={0}; CompaniaId={1}; Anio={2}; SolicitudId={3};",
                usuarioId,
                companiaId,
                anio,
                solicitudId));

            return GenerarNumeroOrdenInstitucional(anio);
        }

        public string GenerarNumeroOrdenAocrVinculada(int anio, string numeroSolicitudGop, int? codigoSolicitud = null)
        {
            var vinculada = ConstruirNumeroOrdenDesdeNumeroSolicitud(numeroSolicitudGop, anio);
            if (!string.IsNullOrWhiteSpace(vinculada))
            {
                if (!_ordenDao.ExisteNumeroOrden(vinculada))
                {
                    _logger.LogInfo(string.Format(
                        CultureInfo.InvariantCulture,
                        "[ORDEN_NUM][GOP_LINKED] Anio={0}; CodigoSolicitud={1}; NumeroSolicitud={2}; NumeroOrden={3}",
                        anio,
                        codigoSolicitud.HasValue ? codigoSolicitud.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        numeroSolicitudGop ?? string.Empty,
                        vinculada));
                    return vinculada;
                }

                throw new InvalidOperationException("La solicitud ya tiene una orden con el mismo correlativo. Abra la orden existente; no se asignara un numero diferente.");
            }

            return GenerarNumeroOrdenInstitucional(anio);
        }

        public static string ConstruirNumeroOrdenDesdeNumeroSolicitud(string numeroSolicitudGop, int anioFallback)
        {
            if (string.IsNullOrWhiteSpace(numeroSolicitudGop))
            {
                return null;
            }

            var texto = numeroSolicitudGop.Trim().ToUpperInvariant();
            var matchAnio = Regex.Match(texto, @"(?:^|-)(20\d{2})(?:-|$)");
            var matchAocr = Regex.Match(texto, @"AOCR\s*0*(\d+)", RegexOptions.IgnoreCase);
            if (!matchAocr.Success)
            {
                return null;
            }

            var anio = matchAnio.Success ? matchAnio.Groups[1].Value : (anioFallback > 0 ? anioFallback.ToString(CultureInfo.InvariantCulture) : DateTime.Now.Year.ToString(CultureInfo.InvariantCulture));
            int correlativo;
            if (!int.TryParse(matchAocr.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out correlativo) || correlativo <= 0)
            {
                return null;
            }

            return string.Format(CultureInfo.InvariantCulture, "DGAC-OR-{0}-AOCR{1}", anio, correlativo.ToString("D3", CultureInfo.InvariantCulture));
        }

        private bool OrdenPerteneceASolicitud(string numeroOrden, int? codigoSolicitud)
        {
            if (!codigoSolicitud.HasValue || codigoSolicitud.Value <= 0 || string.IsNullOrWhiteSpace(numeroOrden) || string.IsNullOrWhiteSpace(_connectionString))
            {
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    const string sql = @"
                        SELECT COUNT(*)
                        FROM public.aocr_or_orden
                        WHERE numero_orden = @numero_orden
                          AND codigo_solicitud::text = @codigo_solicitud;";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@numero_orden", numeroOrden.Trim());
                        cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud.Value.ToString(CultureInfo.InvariantCulture));
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return false;
            }
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
