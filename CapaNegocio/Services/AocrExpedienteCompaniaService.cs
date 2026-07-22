using System;
using System.Globalization;
using CapaDatos.Services;
using Npgsql;

namespace CapaNegocio.Services
{
    public class AocrExpedienteCompaniaService
    {
        private readonly string _connectionString;
        private readonly ILoggingService _logger;

        public AocrExpedienteCompaniaService()
            : this(new SecureConfigurationService().GetConnectionString("PostgreSQL")
                ?? new SecureConfigurationService().GetConnectionString("AOCRConnection")
                ?? string.Empty)
        {
        }

        public AocrExpedienteCompaniaService(string connectionString)
        {
            _connectionString = connectionString ?? string.Empty;
            _logger = LoggingServiceFactory.Create();
        }

        public int ObtenerOCrearExpedienteId(int usuarioRtId, string companiaCodigo, string companiaNombre, int anio)
        {
            if (usuarioRtId <= 0 || string.IsNullOrWhiteSpace(companiaCodigo))
            {
                return 0;
            }

            if (anio <= 0)
            {
                anio = DateTime.Now.Year;
            }

            companiaCodigo = companiaCodigo.Trim();
            companiaNombre = companiaNombre?.Trim();

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return 0;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    const string sql = @"
                        INSERT INTO public.aocr_expediente_compania (usuario_rt_id, compania_codigo, compania_nombre, anio, estado, fecha_creacion, fecha_actualizacion)
                        VALUES (@usuarioRtId, @companiaCodigo, @companiaNombre, @anio, 'ACTIVO', NOW(), NOW())
                        ON CONFLICT (usuario_rt_id, compania_codigo, anio)
                        DO UPDATE SET 
                            compania_nombre = COALESCE(EXCLUDED.compania_nombre, public.aocr_expediente_compania.compania_nombre),
                            fecha_actualizacion = NOW()
                        RETURNING id;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@usuarioRtId", usuarioRtId);
                        cmd.Parameters.AddWithValue("@companiaCodigo", companiaCodigo);
                        cmd.Parameters.AddWithValue("@companiaNombre", (object)companiaNombre ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@anio", anio);

                        var scalar = cmd.ExecuteScalar();
                        var expedienteId = scalar != null ? Convert.ToInt32(scalar) : 0;

                        _logger.LogInfo(string.Format(
                            CultureInfo.InvariantCulture,
                            "[EXPEDIENTE][OBTENER_O_CREAR] UsuarioId={0}; CompaniaCodigo={1}; Anio={2}; ExpedienteId={3}",
                            usuarioRtId, companiaCodigo, anio, expedienteId));

                        return expedienteId;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return 0;
            }
        }
    }
}
