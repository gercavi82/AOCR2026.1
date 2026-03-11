using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Odbc;
using System.Linq;
using System.Text;
using CapaDatos.Infrastructure;
using CapaDatos.Models;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// Acceso a inspectores institucionales en AS400 (OPINSPECTORES / OPIAR2).
    /// </summary>
    public class InspectorAS400DAO : AS400BaseDAO
    {
        private readonly string _schema;
        private readonly string _tablaInspectores;
        private readonly ILoggingService _logger;

        public InspectorAS400DAO(ISecureConfigurationService configService)
            : base(configService)
        {
            var creds = configService.GetAS400Credentials();
            _schema = (creds.Library ?? creds.Database ?? "DGACDAT").Trim().ToUpperInvariant();
            _tablaInspectores = GetSetting("AS400:InspectoresTable", "OPIAR2").Trim().ToUpperInvariant();
            _logger = LoggingServiceFactory.Create();
        }

        [Obsolete("Use el constructor con ISecureConfigurationService")]
        public InspectorAS400DAO()
            : this(new SecureConfigurationService())
        {
        }

        public List<InspectorAs400Record> ListarActivosPorTipo(string tipo)
        {
            if (string.IsNullOrWhiteSpace(tipo) ||
                string.Equals(tipo.Trim(), "TODOS", StringComparison.OrdinalIgnoreCase))
            {
                return ListarActivosPorTipos(new[] { "OPS", "AIR" });
            }

            return ListarActivosPorTipos(new[] { tipo });
        }

        public List<InspectorAs400Record> ListarActivosPorTipos(IEnumerable<string> tipos)
        {
            var tiposNormalizados = NormalizarTipos(tipos);
            var origen = "DB2";

            _logger.LogInfo("[InspectoresDAO-DB2] Inicio consulta inspectores");
            _logger.LogInfo("[InspectoresDAO-DB2] Origen=" + origen + ", Tabla=" + _schema + "." + _tablaInspectores);
            _logger.LogInfo("[InspectoresDAO-DB2] ConnectionString(sanitizada)=" + SanitizarConnectionString(_connectionString));
            _logger.LogInfo("[InspectoresDAO-DB2] Parametros => estado=AC (fallback: sin estado), tipo=" + (tiposNormalizados.Count == 0 ? "TODOS" : string.Join(",", tiposNormalizados)));

            return ExecuteWithConnection(conn =>
            {
                _logger.LogInfo("[InspectoresDAO-DB2] Conexion DB2 abierta OK");

                // Fallback: algunos ambientes no usan 'AC' como estado activo en OPIES1.
                var inspectores = EjecutarConsultaInspectores(conn, tiposNormalizados, soloActivos: true);
                if (inspectores.Count == 0)
                {
                    _logger.LogWarning("[InspectoresDAO-DB2] Sin resultados con estado AC. Se ejecuta fallback sin filtro de estado.");
                    inspectores = EjecutarConsultaInspectores(conn, tiposNormalizados, soloActivos: false);
                }

                _logger.LogInfo("[InspectoresDAO-DB2] Registros obtenidos desde DB2: " + inspectores.Count);
                for (var i = 0; i < inspectores.Count && i < 5; i++)
                {
                    var row = inspectores[i];
                    _logger.LogInfo("[InspectoresDAO-DB2] Ejemplo[" + i + "] => Cedula=" + (row.Cedula ?? "") + ", Nombre=" + (row.NombreCompleto ?? "") + ", Estado=" + (row.Estado ?? "") + ", Tipo=" + (row.Tipo ?? ""));
                }

                return inspectores;
            });
        }

        public InspectorAs400Record ObtenerActivoPorCedula(string cedula, string tipo = null)
        {
            var cedulaNormalizada = NormalizarCedula(cedula);
            if (string.IsNullOrWhiteSpace(cedulaNormalizada))
            {
                return null;
            }

            var tiposNormalizados = NormalizarTipos(string.IsNullOrWhiteSpace(tipo)
                ? Array.Empty<string>()
                : new[] { tipo });

            return ExecuteWithConnection(conn =>
            {
                var inspector = EjecutarConsultaInspectorPorCedula(conn, cedulaNormalizada, tiposNormalizados, soloActivos: true);
                if (inspector == null)
                {
                    inspector = EjecutarConsultaInspectorPorCedula(conn, cedulaNormalizada, tiposNormalizados, soloActivos: false);
                }

                return inspector;
            });
        }

        private List<InspectorAs400Record> EjecutarConsultaInspectores(OdbcConnection conn, List<string> tiposNormalizados, bool soloActivos)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT TRIM(OPICED) AS OPICED, TRIM(OPINO2) AS OPINO2, TRIM(OPIES1) AS OPIES1, TRIM(OPITIP) AS OPITIP ");
            sql.Append("FROM ").Append(_schema).Append('.').Append(_tablaInspectores).Append(' ');
            sql.Append("WHERE 1 = 1 ");

            if (soloActivos)
            {
                sql.Append("AND UPPER(TRIM(COALESCE(OPIES1, ''))) = ? ");
            }

            if (tiposNormalizados.Count > 0)
            {
                sql.Append("AND OPITIP IN (")
                    .Append(string.Join(",", tiposNormalizados.Select(_ => "?")))
                    .Append(") ");
            }

            sql.Append("ORDER BY OPINO2");

            _logger.LogInfo("[InspectoresDAO-DB2] Query ejecutada: " + sql.ToString());
            _logger.LogInfo("[InspectoresDAO-DB2] Filtros aplicados => soloActivos=" + soloActivos + ", tipos=" + (tiposNormalizados.Count == 0 ? "TODOS" : string.Join(",", tiposNormalizados)));

            using (var cmd = CreateCommand(conn, sql.ToString()))
            {
                if (soloActivos)
                {
                    AddParameter(cmd, "AC", OdbcType.VarChar);
                }

                foreach (var tipo in tiposNormalizados)
                {
                    AddParameter(cmd, tipo, OdbcType.VarChar);
                }

                var inspectores = new List<InspectorAs400Record>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = new InspectorAs400Record
                        {
                            Cedula = GetString(reader, 0),
                            NombreCompleto = GetString(reader, 1),
                            Estado = GetString(reader, 2),
                            Tipo = GetString(reader, 3)
                        };

                        if (!string.IsNullOrWhiteSpace(item.Cedula) ||
                            !string.IsNullOrWhiteSpace(item.NombreCompleto))
                        {
                            inspectores.Add(item);
                        }
                    }
                }

                return inspectores;
            }
        }

        private InspectorAs400Record EjecutarConsultaInspectorPorCedula(OdbcConnection conn, string cedulaNormalizada, List<string> tiposNormalizados, bool soloActivos)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT TRIM(OPICED) AS OPICED, TRIM(OPINO2) AS OPINO2, TRIM(OPIES1) AS OPIES1, TRIM(OPITIP) AS OPITIP ");
            sql.Append("FROM ").Append(_schema).Append('.').Append(_tablaInspectores).Append(' ');
            sql.Append("WHERE OPICED = ? ");

            if (soloActivos)
            {
                sql.Append("AND UPPER(TRIM(COALESCE(OPIES1, ''))) = ? ");
            }

            if (tiposNormalizados.Count > 0)
            {
                sql.Append("AND OPITIP IN (")
                    .Append(string.Join(",", tiposNormalizados.Select(_ => "?")))
                    .Append(") ");
            }

            sql.Append("FETCH FIRST 1 ROW ONLY");

            _logger.LogInfo("[InspectoresDAO-DB2] Query obtener por cedula: " + sql.ToString());
            _logger.LogInfo("[InspectoresDAO-DB2] Parametros => cedula=" + cedulaNormalizada + ", soloActivos=" + soloActivos + ", tipos=" + (tiposNormalizados.Count == 0 ? "TODOS" : string.Join(",", tiposNormalizados)));

            using (var cmd = CreateCommand(conn, sql.ToString()))
            {
                AddParameter(cmd, cedulaNormalizada, OdbcType.VarChar);

                if (soloActivos)
                {
                    AddParameter(cmd, "AC", OdbcType.VarChar);
                }

                foreach (var tipoNormalizado in tiposNormalizados)
                {
                    AddParameter(cmd, tipoNormalizado, OdbcType.VarChar);
                }

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        _logger.LogWarning("[InspectoresDAO-DB2] Inspector no encontrado para cedula=" + cedulaNormalizada + ", soloActivos=" + soloActivos);
                        return null;
                    }

                    return new InspectorAs400Record
                    {
                        Cedula = GetString(reader, 0),
                        NombreCompleto = GetString(reader, 1),
                        Estado = GetString(reader, 2),
                        Tipo = GetString(reader, 3)
                    };
                }
            }
        }

        private static List<string> NormalizarTipos(IEnumerable<string> tipos)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (tipos == null)
            {
                return new List<string>();
            }

            foreach (var tipo in tipos)
            {
                if (string.IsNullOrWhiteSpace(tipo))
                {
                    continue;
                }

                var normalizado = tipo.Trim().ToUpperInvariant();
                if (normalizado == "OPS" || normalizado == "AIR")
                {
                    set.Add(normalizado);
                }
            }

            return set.ToList();
        }

        private static string NormalizarCedula(string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula))
            {
                return string.Empty;
            }

            var valor = cedula.Trim();
            return valor.Length > 20 ? valor.Substring(0, 20) : valor;
        }

        private static string GetSetting(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string SanitizarConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return "(vacia)";
            }

            try
            {
                var builder = new OdbcConnectionStringBuilder(connectionString);
                if (builder.ContainsKey("Pwd")) builder["Pwd"] = "****";
                if (builder.ContainsKey("Password")) builder["Password"] = "****";
                if (builder.ContainsKey("UID")) builder["UID"] = "****";
                if (builder.ContainsKey("User ID")) builder["User ID"] = "****";
                return builder.ConnectionString;
            }
            catch
            {
                return connectionString
                    .Replace("Pwd=", "Pwd=****")
                    .Replace("PWD=", "PWD=****")
                    .Replace("Password=", "Password=****")
                    .Replace("UID=", "UID=****")
                    .Replace("User ID=", "User ID=****");
            }
        }
    }
}
