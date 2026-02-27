using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Odbc;
using System.Globalization;
using System.Linq;
using CapaDatos.Infrastructure;
using CapaDatos.Models;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    public class UsuarioAS400DAO : AS400BaseDAO
    {
        private readonly string _schema;
        private readonly string _tablaUsuario;
        private readonly string _tablaAdicional;

        public UsuarioAS400DAO(ISecureConfigurationService configService) : base(configService)
        {
            var creds = configService.GetAS400Credentials();
            _schema = (creds.Library ?? creds.Database ?? "DGACDAT").Trim().ToUpperInvariant();
            _tablaUsuario = GetSetting("AS400:UsuarioTable", "USUARC").Trim().ToUpperInvariant();
            _tablaAdicional = GetSetting("AS400:UsuarioAdicionalTable", "USUAR1").Trim().ToUpperInvariant();
        }

        // Constructor legacy para compatibilidad (usar solo en desarrollo)
        [Obsolete("Use el constructor con ISecureConfigurationService")]
        public UsuarioAS400DAO() : base(new SecureConfigurationService())
        {
            var creds = new SecureConfigurationService().GetAS400Credentials();
            _schema = (creds.Library ?? creds.Database ?? "DGACDAT").Trim().ToUpperInvariant();
            _tablaUsuario = GetSetting("AS400:UsuarioTable", "USUARC").Trim().ToUpperInvariant();
            _tablaAdicional = GetSetting("AS400:UsuarioAdicionalTable", "USUAR1").Trim().ToUpperInvariant();
        }

        public bool UpsertUsuarioCompleto(UsuarioAs400Record record, out string error)
        {
            error = null;

            if (record == null || string.IsNullOrWhiteSpace(record.CodigoUsuario))
            {
                error = "El código de usuario es requerido para registrar en AS400.";
                return false;
            }

            try
            {
                ExecuteWithConnection(conn =>
                {
                    var columnasUsuario = GetColumnas(conn, _schema, _tablaUsuario);
                    if (columnasUsuario.Count == 0)
                    {
                        throw new InvalidOperationException($"No se encontraron columnas en {_schema}.{_tablaUsuario}.");
                    }

                    var fecha = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                    var hora = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);

                    var valoresUsuario = ConstruirValoresUsuario(record, fecha, hora);
                    Upsert(conn, _schema, _tablaUsuario, "USUCOD", valoresUsuario, columnasUsuario);

                    var columnasAdicional = GetColumnas(conn, _schema, _tablaAdicional);
                    if (columnasAdicional.Count > 0)
                    {
                        var valoresAdicional = ConstruirValoresAdicional(record, fecha, hora);
                        var pkAdicional = columnasAdicional.Contains("USUCO8")
                            ? "USUCO8"
                            : (columnasAdicional.Contains("USUCOD") ? "USUCOD" : null);

                        if (!string.IsNullOrWhiteSpace(pkAdicional))
                        {
                            Upsert(conn, _schema, _tablaAdicional, pkAdicional, valoresAdicional, columnasAdicional);
                        }
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private Dictionary<string, object> ConstruirValoresUsuario(UsuarioAs400Record record, string fecha, string hora)
        {
            // Límites según SNAP USUARC
            var usuario    = SafeString(record.UsuarioAuditoria, 10, "AOCR");
            var dispositivo = SafeString(record.Dispositivo, 15, "WEB");

            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["USUCOD"] = SafeString(record.CodigoUsuario,      10),
                ["USUNOM"] = SafeString(record.Nombres,            40),
                ["USUAPE"] = SafeString(record.Apellidos,          40),
                ["USUTIP"] = SafeString(record.TipoIdentificacion,  2),
                ["USUCED"] = SafeString(record.Identificacion,     10),
                ["USUCOR"] = SafeString(record.Correo,            100),
                ["USUCLA"] = SafeString(record.ClaveHash,         256),
                ["USUEST"] = SafeString(record.Estado,              2, "AC"),
                ["USUTI1"] = SafeString(record.TipoApp,             4, "WEB"),
                ["USUIDE"] = SafeString(record.TipoTributario,      3),
                ["USUNUM"] = SafeString(record.NumeroRuc,          20),
                ["USUCO4"] = SafeString(record.RolCodigo,           4),
                ["USUCO5"] = SafeString(record.CiudadCodigo,        4),
                ["USUCO6"] = SafeString(record.DependenciaCodigo,   4),
                ["USUUSU"] = usuario,
                ["USUFEC"] = fecha,   // A08 — yyyyMMdd = 8 chars exactos
                ["USUHOR"] = hora,    // A08 — HHmmss   = 6 chars (OK, <8)
                ["USUDIS"] = dispositivo,
                ["USUUS1"] = usuario,
                ["USUFE1"] = fecha,
                ["USUHO1"] = hora,
                ["USUDI1"] = dispositivo
            };
        }

        private Dictionary<string, object> ConstruirValoresAdicional(UsuarioAs400Record record, string fecha, string hora)
        {
            // Límites según SNAP USUAR1
            var usuario    = SafeString(record.UsuarioAuditoria, 10, "AOCR");
            var dispositivo = SafeString(record.Dispositivo, 15, "WEB");
            var codigo     = SafeString(record.CodigoUsuario, 10);

            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["USUCO8"] = codigo,
                ["USUTIT"] = SafeString(record.Titulo1,             6),
                ["USITI2"] = SafeString(record.Titulo2,             6),  // SNAP: USITI2 (no USUTI2)
                ["USUNO1"] = SafeString(record.NombreCorto,        60),
                ["USUCAR"] = SafeString(record.Cargo,              60),
                ["USUNU1"] = SafeString(record.Telefono1,          20),
                ["USUNU2"] = SafeString(record.Telefono2,          20),
                ["USUCO7"] = SafeString(record.CorreoAdicional,    60),
                ["USUOID"] = record.OidCentroContable.HasValue ? (object)record.OidCentroContable.Value : 0m,
                ["USUCO9"] = SafeString(record.CiudadCodigoAdicional ?? record.CiudadCodigo, 4),
                // Auditoría creación — USUAR1 usa USUUS2/USUFE2/USUHO2/USUDI2
                ["USUUS2"] = usuario,
                ["USUFE2"] = fecha,
                ["USUHO2"] = hora,
                ["USUDI2"] = dispositivo,
                // Auditoría modificación — USUAR1 usa USUUS3/USUFE3/USUHO3/USUDI3
                ["USUUS3"] = usuario,
                ["USUFE3"] = fecha,
                ["USUHO3"] = hora,
                ["USUDI3"] = dispositivo
            };
        }

        private void Upsert(
            OdbcConnection conn,
            string schema,
            string table,
            string pkColumn,
            Dictionary<string, object> valores,
            HashSet<string> columnasDisponibles)
        {
            if (!columnasDisponibles.Contains(pkColumn))
            {
                throw new InvalidOperationException($"La columna PK {pkColumn} no existe en {schema}.{table}.");
            }

            var columnas = valores.Keys
                .Where(c => columnasDisponibles.Contains(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!columnas.Contains(pkColumn))
            {
                columnas.Insert(0, pkColumn);
                valores[pkColumn] = valores.ContainsKey(pkColumn) ? valores[pkColumn] : SafeString(null);
            }

            var existe = ExisteRegistro(conn, schema, table, pkColumn, valores[pkColumn]);

            if (existe)
            {
                // AS400 es la fuente de verdad: si ya existe, no sobrescribir datos
                return;
            }
            else
            {
                var colsInsert = string.Join(", ", columnas);
                var placeholders = string.Join(", ", columnas.Select(_ => "?"));
                var sqlInsert = $"INSERT INTO {schema}.{table} ({colsInsert}) VALUES ({placeholders})";

                using (var cmd = new OdbcCommand(sqlInsert, conn))
                {
                    foreach (var col in columnas)
                    {
                        AddParameter(cmd, valores[col], GetOdbcType(valores[col]));
                    }
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private bool ExisteRegistro(OdbcConnection conn, string schema, string table, string pkColumn, object pkValue)
        {
            var sql = $"SELECT 1 FROM {schema}.{table} WHERE {pkColumn} = ? FETCH FIRST 1 ROWS ONLY";
            using (var cmd = new OdbcCommand(sql, conn))
            {
                AddParameter(cmd, pkValue, GetOdbcType(pkValue));
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value;
            }
        }

        private HashSet<string> GetColumnas(OdbcConnection conn, string schema, string table)
        {
            var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var sql = @"
                    SELECT COLUMN_NAME
                    FROM QSYS2.SYSCOLUMNS
                    WHERE TABLE_SCHEMA = ?
                      AND TABLE_NAME = ?";
                using (var cmd = new OdbcCommand(sql, conn))
                {
                    AddParameter(cmd, schema.ToUpperInvariant(), OdbcType.VarChar);
                    AddParameter(cmd, table.ToUpperInvariant(), OdbcType.VarChar);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                columnas.Add(reader.GetString(0).Trim().ToUpperInvariant());
                            }
                        }
                    }
                }
            }
            catch
            {
                // Si falla la consulta de columnas, devolvemos vacío para manejarlo arriba.
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return columnas;
        }

        private static string SafeString(string value, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            return value.Trim();
        }

        // Trunca al máximo permitido por el campo AS400 según SNAP
        private static string SafeString(string value, int maxLength, string fallback = "")
        {
            var s = SafeString(value, fallback);
            return s.Length > maxLength ? s.Substring(0, maxLength) : s;
        }

        private static OdbcType GetOdbcType(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return OdbcType.VarChar;
            }

            if (value is int || value is long || value is short)
            {
                return OdbcType.Int;
            }

            if (value is decimal || value is float || value is double)
            {
                return OdbcType.Numeric;
            }

            if (value is DateTime)
            {
                return OdbcType.DateTime;
            }

            return OdbcType.VarChar;
        }

        private static string GetSetting(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
