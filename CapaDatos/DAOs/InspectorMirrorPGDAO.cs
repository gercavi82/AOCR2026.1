using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Npgsql;
using CapaDatos.Infrastructure;
using CapaDatos.Models;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    public class InspectorMirrorDiagnostic
    {
        public bool TablaExiste { get; set; }
        public string Tabla { get; set; }
        public int TotalRegistros { get; set; }
        public DateTime? UltimaActualizacion { get; set; }
        public List<InspectorAs400Record> Muestra { get; set; }
        public List<string> CedulasFaltantesEnPg { get; set; }
    }

    public class InspectorMirrorSyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int SourceCount { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public DateTime ExecutedAtUtc { get; set; }
    }

    /// <summary>
    /// Diagnostico de espejo de inspectores en PostgreSQL (si existe tabla espejo).
    /// </summary>
    public class InspectorMirrorPGDAO
    {
        private readonly string _connectionString;
        private readonly ILoggingService _logger;

        public InspectorMirrorPGDAO()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;
            _logger = LoggingServiceFactory.Create();
        }

        public InspectorMirrorDiagnostic DiagnosticarEspejo(List<InspectorAs400Record> inspectoresDb2 = null)
        {
            var tabla = "public.aocr_tbinspectores";
            var cedulasPg = new List<string>();
            var diagnostico = new InspectorMirrorDiagnostic
            {
                Tabla = tabla,
                Muestra = new List<InspectorAs400Record>(),
                CedulasFaltantesEnPg = new List<string>()
            };

            _logger.LogInfo("[InspectoresDAO-PG] Inicio consulta espejo inspectores");
            _logger.LogInfo("[InspectoresDAO-PG] Tabla espejo: " + tabla);

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();

                    const string sqlExisteTabla = @"
                        SELECT EXISTS (
                            SELECT 1
                            FROM information_schema.tables
                            WHERE table_schema='public' AND table_name='aocr_tbinspectores'
                        );";

                    using (var cmdExiste = new NpgsqlCommand(sqlExisteTabla, cn))
                    {
                        diagnostico.TablaExiste = Convert.ToBoolean(cmdExiste.ExecuteScalar());
                    }

                    if (!diagnostico.TablaExiste)
                    {
                        _logger.LogWarning("[InspectoresDAO-PG] No existe la tabla espejo aocr_tbinspectores.");
                        return diagnostico;
                    }

                    const string sqlConteo = @"
                        SELECT COUNT(*)
                        FROM public.aocr_tbinspectores;";

                    using (var cmdConteo = new NpgsqlCommand(sqlConteo, cn))
                    {
                        diagnostico.TotalRegistros = Convert.ToInt32(cmdConteo.ExecuteScalar());
                    }

                    _logger.LogInfo("[InspectoresDAO-PG] Registros en PostgreSQL: " + diagnostico.TotalRegistros);

                    var tieneUpdatedAt = ExisteColumna(cn, "aocr_tbinspectores", "updated_at");
                    if (tieneUpdatedAt)
                    {
                        const string sqlUltimaSync = @"
                            SELECT MAX(updated_at)
                            FROM public.aocr_tbinspectores;";

                        using (var cmdUltimo = new NpgsqlCommand(sqlUltimaSync, cn))
                        {
                            var value = cmdUltimo.ExecuteScalar();
                            if (value != null && value != DBNull.Value)
                            {
                                diagnostico.UltimaActualizacion = Convert.ToDateTime(value);
                            }
                        }

                        _logger.LogInfo("[InspectoresDAO-PG] Ultimo timestamp espejo: " + (diagnostico.UltimaActualizacion.HasValue ? diagnostico.UltimaActualizacion.Value.ToString("yyyy-MM-dd HH:mm:ss") : "N/A"));
                    }
                    else
                    {
                        _logger.LogWarning("[InspectoresDAO-PG] La tabla espejo no tiene columna updated_at para timestamp de sincronizacion.");
                    }

                    const string sqlMuestra = @"
                        SELECT TRIM(COALESCE(cedula,'')) AS cedula,
                               TRIM(COALESCE(nombre_completo,'')) AS nombre,
                               TRIM(COALESCE(estado,'')) AS estado,
                               TRIM(COALESCE(tipo,'')) AS tipo
                        FROM public.aocr_tbinspectores
                        ORDER BY nombre_completo
                        LIMIT 5;";

                    using (var cmdMuestra = new NpgsqlCommand(sqlMuestra, cn))
                    using (var rd = cmdMuestra.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            diagnostico.Muestra.Add(new InspectorAs400Record
                            {
                                Cedula = rd.IsDBNull(0) ? null : rd.GetString(0),
                                NombreCompleto = rd.IsDBNull(1) ? null : rd.GetString(1),
                                Estado = rd.IsDBNull(2) ? null : rd.GetString(2),
                                Tipo = rd.IsDBNull(3) ? null : rd.GetString(3)
                            });
                        }
                    }

                    const string sqlCedulas = @"
                        SELECT TRIM(COALESCE(cedula,'')) AS cedula
                        FROM public.aocr_tbinspectores
                        WHERE TRIM(COALESCE(cedula,'')) <> '';";

                    using (var cmdCedulas = new NpgsqlCommand(sqlCedulas, cn))
                    using (var rdCedulas = cmdCedulas.ExecuteReader())
                    {
                        while (rdCedulas.Read())
                        {
                            if (!rdCedulas.IsDBNull(0))
                            {
                                cedulasPg.Add(rdCedulas.GetString(0));
                            }
                        }
                    }
                }

                if (inspectoresDb2 != null && inspectoresDb2.Count > 0)
                {
                    CompararDb2VsPg(inspectoresDb2, cedulasPg, diagnostico);
                }

                return diagnostico;
            }
            catch (Exception ex)
            {
                _logger.LogError("[InspectoresDAO-PG] Error en diagnostico de espejo: " + ex);
                return diagnostico;
            }
        }

        public bool ExisteInspectorActivoEnPg(string cedula, string tipo)
        {
            if (string.IsNullOrWhiteSpace(cedula))
            {
                return false;
            }

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();

                    if (!ExisteTabla(cn, "aocr_tbinspectores"))
                    {
                        _logger.LogWarning("[InspectoresDAO-PG] Tabla espejo aocr_tbinspectores no existe. Se omite validacion de inspector PG.");
                        return false;
                    }

                    const string sql = @"
                        SELECT EXISTS (
                            SELECT 1
                            FROM public.aocr_tbinspectores
                            WHERE TRIM(COALESCE(cedula,'')) = @cedula
                              AND UPPER(TRIM(COALESCE(estado,''))) = 'AC'
                              AND (@tipo = '' OR UPPER(TRIM(COALESCE(tipo,''))) = @tipo)
                        );";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@cedula", cedula.Trim());
                        cmd.Parameters.AddWithValue("@tipo", (tipo ?? string.Empty).Trim().ToUpperInvariant());
                        return Convert.ToBoolean(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[InspectoresDAO-PG] Error validando inspector en PG cedula=" + cedula + ": " + ex);
                return false;
            }
        }

        public InspectorMirrorSyncResult SincronizarDesdeDb2()
        {
            try
            {
                var source = new InspectorAS400DAO(new SecureConfigurationService());
                var inspectores = source.ListarActivosPorTipos(new[] { "OPS", "AIR" });
                return SincronizarDesdeDb2(inspectores);
            }
            catch (Exception ex)
            {
                _logger.LogError("[Sync-Inspectores] Error preparando sync DB2->PG: " + ex);
                return new InspectorMirrorSyncResult
                {
                    Success = false,
                    Message = "No se pudo obtener datos de inspectores desde DB2: " + ex.Message,
                    ExecutedAtUtc = DateTime.UtcNow
                };
            }
        }

        public InspectorMirrorSyncResult SincronizarDesdeDb2(List<InspectorAs400Record> inspectoresDb2)
        {
            var result = new InspectorMirrorSyncResult
            {
                ExecutedAtUtc = DateTime.UtcNow,
                Success = false
            };

            if (inspectoresDb2 == null)
            {
                result.Message = "No se recibio la lista de inspectores de origen.";
                return result;
            }

            var normalizados = inspectoresDb2
                .Where(x => x != null)
                .Select(x => new InspectorAs400Record
                {
                    Cedula = (x.Cedula ?? string.Empty).Trim(),
                    NombreCompleto = (x.NombreCompleto ?? string.Empty).Trim(),
                    Estado = string.IsNullOrWhiteSpace(x.Estado) ? "AC" : x.Estado.Trim().ToUpperInvariant(),
                    Tipo = (x.Tipo ?? string.Empty).Trim().ToUpperInvariant()
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Cedula))
                .GroupBy(x => x.Cedula, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            result.SourceCount = normalizados.Count;

            if (normalizados.Count == 0)
            {
                result.Success = true;
                result.Message = "No hay inspectores validos para sincronizar.";
                return result;
            }

            _logger.LogInfo("[Sync-Inspectores] Inicio sync real DB2 -> public.aocr_tbinspectores. Registros origen=" + normalizados.Count);

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();

                    if (!ExisteTabla(cn, "aocr_tbinspectores"))
                    {
                        result.Message = "No existe la tabla public.aocr_tbinspectores.";
                        _logger.LogWarning("[Sync-Inspectores] " + result.Message);
                        return result;
                    }

                    var tieneCreatedAt = ExisteColumna(cn, "aocr_tbinspectores", "created_at");
                    var tieneUpdatedAt = ExisteColumna(cn, "aocr_tbinspectores", "updated_at");

                    using (var tx = cn.BeginTransaction())
                    {
                        foreach (var item in normalizados)
                        {
                            try
                            {
                                var updateSql = @"
                                    UPDATE public.aocr_tbinspectores
                                    SET nombre_completo = @nombre,
                                        estado = @estado,
                                        tipo = @tipo" + (tieneUpdatedAt ? ", updated_at = NOW()" : string.Empty) + @"
                                    WHERE TRIM(COALESCE(cedula,'')) = @cedula;";

                                using (var cmdUpdate = new NpgsqlCommand(updateSql, cn, tx))
                                {
                                    cmdUpdate.Parameters.AddWithValue("@nombre", (object)item.NombreCompleto ?? string.Empty);
                                    cmdUpdate.Parameters.AddWithValue("@estado", (object)item.Estado ?? string.Empty);
                                    cmdUpdate.Parameters.AddWithValue("@tipo", (object)item.Tipo ?? string.Empty);
                                    cmdUpdate.Parameters.AddWithValue("@cedula", item.Cedula);

                                    var rows = cmdUpdate.ExecuteNonQuery();
                                    if (rows > 0)
                                    {
                                        result.Updated += rows;
                                        continue;
                                    }
                                }

                                var insertColumns = "cedula, nombre_completo, estado, tipo";
                                var insertValues = "@cedula, @nombre, @estado, @tipo";

                                if (tieneCreatedAt)
                                {
                                    insertColumns += ", created_at";
                                    insertValues += ", NOW()";
                                }

                                if (tieneUpdatedAt)
                                {
                                    insertColumns += ", updated_at";
                                    insertValues += ", NOW()";
                                }

                                var insertSql = "INSERT INTO public.aocr_tbinspectores (" + insertColumns + ") VALUES (" + insertValues + ");";
                                using (var cmdInsert = new NpgsqlCommand(insertSql, cn, tx))
                                {
                                    cmdInsert.Parameters.AddWithValue("@cedula", item.Cedula);
                                    cmdInsert.Parameters.AddWithValue("@nombre", (object)item.NombreCompleto ?? string.Empty);
                                    cmdInsert.Parameters.AddWithValue("@estado", (object)item.Estado ?? string.Empty);
                                    cmdInsert.Parameters.AddWithValue("@tipo", (object)item.Tipo ?? string.Empty);
                                    result.Inserted += cmdInsert.ExecuteNonQuery();
                                }
                            }
                            catch (Exception exRow)
                            {
                                result.Errors++;
                                _logger.LogError("[Sync-Inspectores] Error sincronizando cedula=" + item.Cedula + ": " + exRow);
                            }
                        }

                        tx.Commit();
                    }
                }

                result.Skipped = Math.Max(0, result.SourceCount - (result.Inserted + result.Updated + result.Errors));
                result.Success = result.Errors == 0;
                result.Message = string.Format(
                    "Sync inspectores RT finalizado. source={0} inserted={1} updated={2} skipped={3} errors={4}",
                    result.SourceCount, result.Inserted, result.Updated, result.Skipped, result.Errors);

                _logger.LogInfo("[Sync-Inspectores] " + result.Message);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "Error ejecutando sync de inspectores RT: " + ex.Message;
                _logger.LogError("[Sync-Inspectores] " + ex);
                return result;
            }
        }

        private static bool ExisteTabla(NpgsqlConnection cn, string tabla)
        {
            const string sql = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema='public'
                      AND table_name=@tabla
                );";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                return Convert.ToBoolean(cmd.ExecuteScalar());
            }
        }

        private void CompararDb2VsPg(List<InspectorAs400Record> inspectoresDb2, List<string> cedulasPg, InspectorMirrorDiagnostic diagnostico)
        {
            var inicio = DateTime.UtcNow;
            _logger.LogInfo("[Sync-Inspectores] Inicio sincronizacion DB2 -> PostgreSQL (modo diagnostico)");

            try
            {
                var db2Cedulas = new HashSet<string>(
                    inspectoresDb2
                        .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Cedula))
                        .Select(x => x.Cedula.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                var pgCedulas = new HashSet<string>(
                    (cedulasPg ?? new List<string>())
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                var faltantes = db2Cedulas.Where(c => !pgCedulas.Contains(c)).Take(20).ToList();
                diagnostico.CedulasFaltantesEnPg = faltantes;

                var insertados = Math.Max(0, db2Cedulas.Count - pgCedulas.Count);
                var actualizados = Math.Min(db2Cedulas.Count, pgCedulas.Count);
                var omitidos = 0;

                _logger.LogInfo("[Sync-Inspectores] Registros DB2: " + db2Cedulas.Count);
                _logger.LogInfo("[Sync-Inspectores] Registros PG (muestra diagnostica): " + pgCedulas.Count);
                _logger.LogInfo("[InspectoresDAO-PG] Diferencia detectada vs DB2: DB2=" + db2Cedulas.Count + " / PG=" + diagnostico.TotalRegistros);
                _logger.LogInfo("[Sync-Inspectores] Insertados en PG (estimado): " + insertados);
                _logger.LogInfo("[Sync-Inspectores] Actualizados en PG (estimado): " + actualizados);
                _logger.LogInfo("[Sync-Inspectores] Omitidos (estimado): " + omitidos);

                if (faltantes.Count > 0)
                {
                    _logger.LogWarning("[Sync-Inspectores] Cedulas faltantes en PG (muestra): " + string.Join(",", faltantes));
                }

                var duracionMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;
                _logger.LogInfo("[Sync-Inspectores] Fin OK. TiempoMs=" + duracionMs);
            }
            catch (Exception ex)
            {
                _logger.LogError("[Sync-Inspectores] Error en comparacion DB2->PG: " + ex);
            }
        }

        /// <summary>
        /// Busca un inspector en el espejo PG por cédula (caso exacto, normalizado).
        /// Devuelve null si la tabla no existe o no se encuentra el registro.
        /// </summary>
        public InspectorAs400Record ObtenerPorCedula(string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula))
                return null;

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();

                    if (!ExisteTabla(cn, "aocr_tbinspectores"))
                        return null;

                    const string sql = @"
                        SELECT TRIM(COALESCE(cedula,'')),
                               TRIM(COALESCE(nombre_completo,'')),
                               TRIM(COALESCE(estado,'')),
                               TRIM(COALESCE(tipo,''))
                        FROM public.aocr_tbinspectores
                        WHERE LOWER(TRIM(COALESCE(cedula,''))) = LOWER(TRIM(@cedula))
                        LIMIT 1;";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@cedula", cedula.Trim());
                        using (var rd = cmd.ExecuteReader())
                        {
                            if (!rd.Read())
                                return null;

                            return new InspectorAs400Record
                            {
                                Cedula         = rd.IsDBNull(0) ? null : rd.GetString(0),
                                NombreCompleto = rd.IsDBNull(1) ? null : rd.GetString(1),
                                Estado         = rd.IsDBNull(2) ? null : rd.GetString(2),
                                Tipo           = rd.IsDBNull(3) ? null : rd.GetString(3)
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[InspectoresDAO-PG] Error en ObtenerPorCedula cedula=" + cedula + ": " + ex);
                return null;
            }
        }

        private static bool ExisteColumna(NpgsqlConnection cn, string tabla, string columna)
        {
            const string sql = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema='public'
                      AND table_name=@tabla
                      AND column_name=@columna
                );";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                cmd.Parameters.AddWithValue("@columna", columna);
                return Convert.ToBoolean(cmd.ExecuteScalar());
            }
        }
    }
}
