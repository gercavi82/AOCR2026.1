using System;
using System.Collections.Generic;
using Npgsql;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    public class Fr3OutboxWorkerDAO
    {
        private readonly string _connectionString;
        private readonly ILoggingService _logger;

        public Fr3OutboxWorkerDAO()
        {
            _logger = LoggingServiceFactory.Create();
            var config = System.Configuration.ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _connectionString = config != null ? config.ConnectionString : string.Empty;
        }

        public Fr3OutboxWorkerDAO(string connectionString)
        {
            _logger = LoggingServiceFactory.Create();
            _connectionString = connectionString;
        }

        public List<dynamic> ReclamarEventos(int limit, string workerId, int lockMinutes)
        {
            var items = new List<dynamic>();

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    var sql = @"
                        WITH cte AS (
                            SELECT id FROM aocr_fr3_outbox
                            WHERE estado = 'PENDIENTE' 
                               AND (proximo_intento IS NULL OR proximo_intento <= NOW())
                               AND intentos < 10
                            ORDER BY id ASC
                            LIMIT @limit
                            FOR UPDATE SKIP LOCKED
                        )
                        UPDATE aocr_fr3_outbox o
                        SET estado = 'EN_PROCESO',
                            worker_id = @workerId,
                            lock_until = NOW() + MAKE_INTERVAL(mins => @lockMinutes),
                            updated_at = NOW()
                        FROM cte
                        WHERE o.id = cte.id
                        RETURNING o.id, o.orden_id, o.pago_id, o.intentos, o.payload;
                    ";

                    using (var cmd = new NpgsqlCommand(sql, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@limit", limit);
                        cmd.Parameters.AddWithValue("@workerId", workerId);
                        cmd.Parameters.AddWithValue("@lockMinutes", lockMinutes);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                items.Add(new
                                {
                                    Id = reader.GetInt32(0),
                                    OrdenId = reader.GetInt32(1),
                                    PagoId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                    Intentos = reader.GetInt32(3),
                                    Payload = reader.IsDBNull(4) ? null : reader.GetString(4)
                                });
                            }
                        }
                    }
                    
                    // Recover expired locks
                    var sqlRecover = @"
                        WITH cte AS (
                            SELECT id FROM aocr_fr3_outbox
                            WHERE estado = 'EN_PROCESO' 
                               AND lock_until < NOW()
                            ORDER BY id ASC
                            LIMIT @limit
                            FOR UPDATE SKIP LOCKED
                        )
                        UPDATE aocr_fr3_outbox o
                        SET worker_id = @workerId,
                            lock_until = NOW() + MAKE_INTERVAL(mins => @lockMinutes),
                            updated_at = NOW()
                        FROM cte
                        WHERE o.id = cte.id
                        RETURNING o.id, o.orden_id, o.pago_id, o.intentos, o.payload;
                    ";
                    
                    using (var cmdRec = new NpgsqlCommand(sqlRecover, conn, tx))
                    {
                        cmdRec.Parameters.AddWithValue("@limit", limit - items.Count > 0 ? limit - items.Count : 0);
                        cmdRec.Parameters.AddWithValue("@workerId", workerId);
                        cmdRec.Parameters.AddWithValue("@lockMinutes", lockMinutes);
                        
                        if ((limit - items.Count) > 0)
                        {
                            using (var reader = cmdRec.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    items.Add(new
                                    {
                                        Id = reader.GetInt32(0),
                                        OrdenId = reader.GetInt32(1),
                                        PagoId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                        Intentos = reader.GetInt32(3),
                                        Payload = reader.IsDBNull(4) ? null : reader.GetString(4)
                                    });
                                }
                            }
                        }
                    }

                    tx.Commit();
                }
            }

            return items;
        }

        public void CompletarEvento(int id, string payload)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                var sql = @"
                    UPDATE aocr_fr3_outbox
                    SET estado = 'COMPLETADO',
                        payload = COALESCE(@payload, payload),
                        worker_id = NULL,
                        lock_until = NULL,
                        updated_at = NOW()
                    WHERE id = @id";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@payload", (object)payload ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RegistrarFalloEvento(int id, string errorInfo, int intentos, int backoffMinutos, bool falloDefinitivo)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                var estado = falloDefinitivo || intentos >= 10 ? "ERROR_FINAL" : "PENDIENTE";
                
                var sql = @"
                    UPDATE aocr_fr3_outbox
                    SET estado = @estado,
                        error_last = @errorInfo,
                        intentos = @intentos,
                        proximo_intento = NOW() + MAKE_INTERVAL(mins => @backoff),
                        worker_id = NULL,
                        lock_until = NULL,
                        updated_at = NOW()
                    WHERE id = @id";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@estado", estado);
                    cmd.Parameters.AddWithValue("@errorInfo", (object)errorInfo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@intentos", intentos);
                    cmd.Parameters.AddWithValue("@backoff", backoffMinutos);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
