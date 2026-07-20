using System;
using Npgsql;
using CapaDatos.Entidades;
using CapaDatos.Interfaces;
using CapaDatos.Infrastructure;

namespace CapaDatos.DAOs
{
    public class Fr3OutboxDAO : IFr3OutboxDAO
    {
        private readonly string _connectionString;
        private readonly ILoggerService _logger;

        public Fr3OutboxDAO()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString
                ?? System.Configuration.ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                ?? "";
            _logger = new NLogLoggerService("Fr3OutboxDAO");
        }

        public Fr3OutboxDAO(string connectionString)
        {
            _connectionString = connectionString;
            _logger = new NLogLoggerService("Fr3OutboxDAO");
        }

        public bool EncolarEvento(Fr3OutboxEvent evento)
        {
            if (evento == null || string.IsNullOrWhiteSpace(evento.EventKey)) return false;

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO aocr_fr3_outbox (
                            event_key, orden_id, pago_id, estado, intentos, proximo_intento, payload, created_at, updated_at
                        ) VALUES (
                            @event_key, @orden_id, @pago_id, @estado, @intentos, @proximo_intento, @payload, NOW(), NOW()
                        )
                        ON CONFLICT (event_key) DO NOTHING;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@event_key", evento.EventKey);
                        cmd.Parameters.AddWithValue("@orden_id", evento.OrdenId);
                        cmd.Parameters.AddWithValue("@pago_id", evento.PagoId.HasValue ? (object)evento.PagoId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@estado", string.IsNullOrWhiteSpace(evento.Estado) ? "PENDIENTE" : evento.Estado);
                        cmd.Parameters.AddWithValue("@intentos", evento.Intentos);
                        cmd.Parameters.AddWithValue("@proximo_intento", evento.ProximoIntento.HasValue ? (object)evento.ProximoIntento.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@payload", string.IsNullOrWhiteSpace(evento.Payload) ? (object)DBNull.Value : evento.Payload);

                        int rows = cmd.ExecuteNonQuery();
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en EncolarEvento para event_key: {evento.EventKey}");
                return false;
            }
        }

        public void EncolarOReactivar(int ordenId, int? pagoId, string payload)
        {
            var eventKey = "ORD_" + ordenId + "_PAGO_" + (pagoId ?? 0);
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    INSERT INTO aocr_fr3_outbox (
                        event_key, orden_id, pago_id, estado, intentos, payload, created_at, updated_at
                    ) VALUES (
                        @event_key, @orden_id, @pago_id, 'PENDIENTE', 0, @payload, NOW(), NOW()
                    )
                    ON CONFLICT (event_key) DO UPDATE SET 
                        estado = 'PENDIENTE',
                        intentos = 0,
                        proximo_intento = NULL,
                        worker_id = NULL,
                        lock_until = NULL,
                        updated_at = NOW();";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@event_key", eventKey);
                    cmd.Parameters.AddWithValue("@orden_id", ordenId);
                    cmd.Parameters.AddWithValue("@pago_id", pagoId.HasValue ? (object)pagoId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@payload", (object)payload ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
