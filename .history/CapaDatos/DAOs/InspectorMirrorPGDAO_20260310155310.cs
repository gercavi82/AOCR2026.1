using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Npgsql;
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
