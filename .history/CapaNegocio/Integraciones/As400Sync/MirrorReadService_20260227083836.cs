using System;
using System.Collections.Generic;
using Npgsql;
using CapaNegocio.Services;

namespace CapaNegocio.Integraciones.As400Sync
{
    public class MirrorUsuarioDto
    {
        public string CodigoUsuario { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string Correo { get; set; }
        public string EstadoActividad { get; set; }
        public string CodigoRol { get; set; }
        public string CodigoCiudad { get; set; }
        public string Cargo { get; set; }
        public string NombreCorto { get; set; }
        public DateTime? SourceUpdatedAt { get; set; }
        public DateTime MirrorSyncedAt { get; set; }
    }

    public class MirrorCompaniaDto
    {
        public string CodigoOaci { get; set; }
        public string CodigoIata { get; set; }
        public string CodigoNumeroCia { get; set; }
        public string NombreCompania { get; set; }
    }

    public class MirrorFr3CabeceraDto
    {
        public decimal Secuencial { get; set; }
        public string Aeropuerto { get; set; }
        public string Anio { get; set; }
        public string FechaControlVuelo { get; set; }
        public string TipoOperacion { get; set; }
        public string RutaPlanVuelo { get; set; }
        public int NumAterrizaPais { get; set; }
        public decimal Total { get; set; }
        public decimal GranTotal { get; set; }
        public string Autorizacion { get; set; }
        public string Observacion { get; set; }
        public string Ruc { get; set; }
        public string NombreCliente { get; set; }
        public string Estado { get; set; }
        public string NacInter { get; set; }
        public string NombreCia { get; set; }
        public string Matricula { get; set; }
        public decimal ValorCharter { get; set; }
        public string FormaPago { get; set; }
        public string CodigoBanco { get; set; }
        public string Deposito { get; set; }
        public string NumeroFactura { get; set; }
        public string FechaCreacion { get; set; }
        public string Procesado { get; set; }
        public DateTime MirrorSyncedAt { get; set; }
    }

    public class MirrorSyncStatusDto
    {
        public string Tabla { get; set; }
        public string Estado { get; set; }
        public DateTime? UltimaSync { get; set; }
        public string UltimaClaveSync { get; set; }
        public string UltimoError { get; set; }
        public DateTime ActualizadoEn { get; set; }
    }

    public class MirrorReadService
    {
        private readonly string _connectionString;

        public MirrorReadService()
        {
            var env = As400MirrorSyncOptionsFactory.Create();
            _connectionString = env.PostgresMirrorConnectionString;
        }

        public MirrorUsuarioDto ObtenerUsuarioPorCodigo(string codigoUsuario)
        {
            if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return null;
            }

            const string sql = @"
                SELECT u.usucod, u.usunom, u.usuape, u.usucor, u.usuest, u.usuco4, u.usuco5,
                       a.usucar, a.usuno1, u._source_updated_at, u._mirror_synced_at
                  FROM mirror_raw.usuarc u
             LEFT JOIN mirror_raw.usuar1 a ON a.usuco8 = u.usucod
                 WHERE u.usucod = @codigo
                   AND COALESCE(u._is_deleted, false) = false
                 LIMIT 1";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("codigo", codigoUsuario.Trim());
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return null;

                        return new MirrorUsuarioDto
                        {
                            CodigoUsuario = rd.IsDBNull(0) ? null : rd.GetString(0),
                            Nombres = rd.IsDBNull(1) ? null : rd.GetString(1),
                            Apellidos = rd.IsDBNull(2) ? null : rd.GetString(2),
                            Correo = rd.IsDBNull(3) ? null : rd.GetString(3),
                            EstadoActividad = rd.IsDBNull(4) ? null : rd.GetString(4),
                            CodigoRol = rd.IsDBNull(5) ? null : rd.GetString(5),
                            CodigoCiudad = rd.IsDBNull(6) ? null : rd.GetString(6),
                            Cargo = rd.IsDBNull(7) ? null : rd.GetString(7),
                            NombreCorto = rd.IsDBNull(8) ? null : rd.GetString(8),
                            SourceUpdatedAt = rd.IsDBNull(9) ? (DateTime?)null : rd.GetDateTime(9),
                            MirrorSyncedAt = rd.IsDBNull(10) ? DateTime.MinValue : rd.GetDateTime(10)
                        };
                    }
                }
            }
            catch (PostgresException ex)
            {
                // Mirror no desplegado todavía: fallback silencioso para no romper AOCR
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerUsuarioPorCodigo no disponible: " + ex.MessageText, "MirrorReadService");
                return null;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error consultando usuario en espejo", ex.ToString(), "MirrorReadService");
                return null;
            }
        }

        public IList<MirrorCompaniaDto> ListarCompaniasActivas(int take)
        {
            var list = new List<MirrorCompaniaDto>();
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return list;
            }

            if (take <= 0) take = 100;

            const string sql = @"
                SELECT ciacod, ciaco2, ciaco3, cianom
                  FROM mirror_raw.ciaarc
                 WHERE COALESCE(_is_deleted, false) = false
                   AND TRIM(COALESCE(ciaest, '')) = 'AC'
              ORDER BY cianom
                 LIMIT @take";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("take", take);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new MirrorCompaniaDto
                            {
                                CodigoOaci = rd.IsDBNull(0) ? null : rd.GetString(0),
                                CodigoIata = rd.IsDBNull(1) ? null : rd.GetString(1),
                                CodigoNumeroCia = rd.IsDBNull(2) ? null : rd.GetString(2),
                                NombreCompania = rd.IsDBNull(3) ? null : rd.GetString(3)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ListarCompaniasActivas no disponible: " + ex.Message, "MirrorReadService");
            }

            return list;
        }

        /// <summary>
        /// Lista registros FR3 (cabecera) desde el espejo, ordenados por fecha de creacion descendente.
        /// Devuelve lista vacía si el mirror no está disponible (fallback seguro).
        /// </summary>
        public IList<MirrorFr3CabeceraDto> ListarFr3Recientes(int take = 100, string aeropuerto = null, string anio = null)
        {
            var list = new List<MirrorFr3CabeceraDto>();
            if (string.IsNullOrWhiteSpace(_connectionString)) return list;
            if (take <= 0) take = 100;

            var whereParts = new List<string> { "COALESCE(_is_deleted, false) = false" };
            if (!string.IsNullOrWhiteSpace(aeropuerto))
                whereParts.Add("UPPER(TRIM(opcaer)) = UPPER(@aer)");
            if (!string.IsNullOrWhiteSpace(anio))
                whereParts.Add("TRIM(opcano) = @anio");

            var sql = @"
                SELECT opcsec, opcaer, opcano, opcfe4, opctip, opcrut, opcnro,
                       opctot, opcgra, opcaut, opcobs, opcru1, opcno4, opcest,
                       opcnac, opcno5, opcmat, opcva6, opcfor, opcban, opcche,
                       opcnum, opcda4, opcpro, _mirror_synced_at
                  FROM mirror_raw.opcar5
                 WHERE " + string.Join(" AND ", whereParts) + @"
              ORDER BY opcda4 DESC, opch01 DESC, opcsec DESC
                 LIMIT @take";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("take", take);
                    if (!string.IsNullOrWhiteSpace(aeropuerto))
                        cmd.Parameters.AddWithValue("aer", aeropuerto.Trim().ToUpper());
                    if (!string.IsNullOrWhiteSpace(anio))
                        cmd.Parameters.AddWithValue("anio", anio.Trim());

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new MirrorFr3CabeceraDto
                            {
                                Secuencial        = rd.IsDBNull(0) ? 0 : rd.GetDecimal(0),
                                Aeropuerto        = rd.IsDBNull(1) ? null : rd.GetString(1),
                                Anio              = rd.IsDBNull(2) ? null : rd.GetString(2),
                                FechaControlVuelo = rd.IsDBNull(3) ? null : rd.GetString(3),
                                TipoOperacion     = rd.IsDBNull(4) ? null : rd.GetString(4),
                                RutaPlanVuelo     = rd.IsDBNull(5) ? null : rd.GetString(5),
                                NumAterrizaPais   = rd.IsDBNull(6) ? 0 : rd.GetInt32(6),
                                Total             = rd.IsDBNull(7) ? 0m : rd.GetDecimal(7),
                                GranTotal         = rd.IsDBNull(8) ? 0m : rd.GetDecimal(8),
                                Autorizacion      = rd.IsDBNull(9) ? null : rd.GetString(9),
                                Observacion       = rd.IsDBNull(10) ? null : rd.GetString(10),
                                Ruc               = rd.IsDBNull(11) ? null : rd.GetString(11),
                                NombreCliente     = rd.IsDBNull(12) ? null : rd.GetString(12),
                                Estado            = rd.IsDBNull(13) ? null : rd.GetString(13),
                                NacInter          = rd.IsDBNull(14) ? null : rd.GetString(14),
                                NombreCia         = rd.IsDBNull(15) ? null : rd.GetString(15),
                                Matricula         = rd.IsDBNull(16) ? null : rd.GetString(16),
                                ValorCharter      = rd.IsDBNull(17) ? 0m : rd.GetDecimal(17),
                                FormaPago         = rd.IsDBNull(18) ? null : rd.GetString(18),
                                CodigoBanco       = rd.IsDBNull(19) ? null : rd.GetString(19),
                                Deposito          = rd.IsDBNull(20) ? null : rd.GetString(20),
                                NumeroFactura     = rd.IsDBNull(21) ? null : rd.GetString(21),
                                FechaCreacion     = rd.IsDBNull(22) ? null : rd.GetString(22),
                                Procesado         = rd.IsDBNull(23) ? null : rd.GetString(23),
                                MirrorSyncedAt    = rd.IsDBNull(24) ? DateTime.MinValue : rd.GetDateTime(24)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ListarFr3Recientes no disponible: " + ex.Message, "MirrorReadService");
            }

            return list;
        }

        /// <summary>
        /// Obtiene estado actual de watermarks para monitoreo del sync.
        /// Devuelve lista vacía si las tablas sync no existen todavía.
        /// </summary>
        public IList<MirrorSyncStatusDto> ObtenerEstadoSync()
        {
            var list = new List<MirrorSyncStatusDto>();
            if (string.IsNullOrWhiteSpace(_connectionString)) return list;

            const string sql = @"
                SELECT table_name, status, last_success_ts, last_success_key, last_error, updated_at
                  FROM sync.watermark
              ORDER BY table_name";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new MirrorSyncStatusDto
                            {
                                Tabla           = rd.IsDBNull(0) ? null : rd.GetString(0),
                                Estado          = rd.IsDBNull(1) ? null : rd.GetString(1),
                                UltimaSync      = rd.IsDBNull(2) ? (DateTime?)null : rd.GetDateTime(2),
                                UltimaClaveSync = rd.IsDBNull(3) ? null : rd.GetString(3),
                                UltimoError     = rd.IsDBNull(4) ? null : rd.GetString(4),
                                ActualizadoEn   = rd.IsDBNull(5) ? DateTime.MinValue : rd.GetDateTime(5)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerEstadoSync no disponible: " + ex.Message, "MirrorReadService");
            }

            return list;
        }

        /// <summary>
        /// Últimos lotes registrados en sync.batch_log (para dashboard de admin).
        /// </summary>
        public IList<SyncBatchResult> ObtenerUltimosLotes(int take = 30)
        {
            var list = new List<SyncBatchResult>();
            if (string.IsNullOrWhiteSpace(_connectionString)) return list;
            if (take <= 0) take = 30;

            const string sql = @"
                SELECT batch_id, table_name, status, rows_read, rows_applied, rows_rejected, rows_deleted,
                       latency_ms, error, started_at, ended_at
                  FROM sync.batch_log
              ORDER BY started_at DESC
                 LIMIT @take";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("take", take);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var started  = rd.IsDBNull(9) ? DateTime.MinValue : rd.GetDateTime(9);
                            var ended    = rd.IsDBNull(10)? (DateTime?)null : rd.GetDateTime(10);
                            var latencyMs = rd.IsDBNull(7) ? 0 : Convert.ToInt64(rd.GetValue(7));
                            list.Add(new SyncBatchResult
                            {
                                BatchId      = rd.IsDBNull(0) ? Guid.Empty : rd.GetGuid(0),
                                TableName    = rd.IsDBNull(1) ? null : rd.GetString(1),
                                Status       = rd.IsDBNull(2) ? null : rd.GetString(2),
                                RowsRead     = rd.IsDBNull(3) ? 0 : rd.GetInt32(3),
                                RowsApplied  = rd.IsDBNull(4) ? 0 : rd.GetInt32(4),
                                RowsRejected = rd.IsDBNull(5) ? 0 : rd.GetInt32(5),
                                RowsDeleted  = rd.IsDBNull(6) ? 0 : rd.GetInt32(6),
                                Error        = rd.IsDBNull(8) ? null : rd.GetString(8),
                                Duration     = ended.HasValue ? ended.Value - started : TimeSpan.Zero
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogBL.RegistrarAdvertencia("MirrorReadService.ObtenerUltimosLotes no disponible: " + ex.Message, "MirrorReadService");
            }

            return list;
        }
    } // class MirrorReadService
} // namespace