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

        public string ObtenerCodigoCiudadPorCodigoUsuario(string codigoUsuario)
        {
            if (string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return null;
            }

            var codigo = SafeString(codigoUsuario, 10).ToUpperInvariant();

            try
            {
                return ExecuteWithConnection(conn =>
                {
                    var columnasUsuario = GetColumnas(conn, _schema, _tablaUsuario);
                    if (columnasUsuario.Contains("USUCO5"))
                    {
                        var ciudad = ObtenerCampoPorPk(conn, _schema, _tablaUsuario, "USUCOD", codigo, "USUCO5");
                        if (!string.IsNullOrWhiteSpace(ciudad))
                        {
                            return ciudad;
                        }
                    }

                    var columnasAdicional = GetColumnas(conn, _schema, _tablaAdicional);
                    var pkAdicional = columnasAdicional.Contains("USUCO8")
                        ? "USUCO8"
                        : (columnasAdicional.Contains("USUCOD") ? "USUCOD" : null);
                    if (!string.IsNullOrWhiteSpace(pkAdicional))
                    {
                        if (columnasAdicional.Contains("USUCO9"))
                        {
                            var ciudadAdicional = ObtenerCampoPorPk(conn, _schema, _tablaAdicional, pkAdicional, codigo, "USUCO9");
                            if (!string.IsNullOrWhiteSpace(ciudadAdicional))
                            {
                                return ciudadAdicional;
                            }
                        }

                        if (columnasAdicional.Contains("USUCO5"))
                        {
                            var ciudadAlterna = ObtenerCampoPorPk(conn, _schema, _tablaAdicional, pkAdicional, codigo, "USUCO5");
                            if (!string.IsNullOrWhiteSpace(ciudadAlterna))
                            {
                                return ciudadAlterna;
                            }
                        }
                    }

                    return null;
                });
            }
            catch
            {
                return null;
            }
        }

        public UsuarioInternoAs400Info ObtenerDatosUsuarioInterno(string codigoUsuario)
        {
            if (string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return null;
            }

            var codigoEntrada = SafeString(codigoUsuario, 10).ToUpperInvariant();

            try
            {
                return ExecuteWithConnection(conn =>
                {
                    var codigo = ResolverCodigoUsuarioInterno(conn, codigoEntrada);
                    if (string.IsNullOrWhiteSpace(codigo))
                    {
                        return null;
                    }

                    var columnasUsuario = GetColumnas(conn, _schema, _tablaUsuario);
                    var columnasAdicional = GetColumnas(conn, _schema, _tablaAdicional);

                    var pkAdicional = columnasAdicional.Contains("USUCO8")
                        ? "USUCO8"
                        : (columnasAdicional.Contains("USUCOD") ? "USUCOD" : null);

                    var existeUsuario = ObtenerCampoPorPkSeguro(conn, _schema, _tablaUsuario, "USUCOD", codigo, "USUCOD");
                    if (string.IsNullOrWhiteSpace(existeUsuario) && !string.IsNullOrWhiteSpace(pkAdicional))
                    {
                        existeUsuario = ObtenerCampoPorPkSeguro(conn, _schema, _tablaAdicional, pkAdicional, codigo, pkAdicional);
                    }

                    if (string.IsNullOrWhiteSpace(existeUsuario))
                    {
                        return null;
                    }

                    var ciudad = string.Empty;
                    if (!string.IsNullOrWhiteSpace(pkAdicional) && columnasAdicional.Contains("USUCO9"))
                    {
                        ciudad = ObtenerCampoPorPkSeguro(conn, _schema, _tablaAdicional, pkAdicional, codigo, "USUCO9");
                    }

                    if (string.IsNullOrWhiteSpace(ciudad) && columnasUsuario.Contains("USUCO5"))
                    {
                        ciudad = ObtenerCampoPorPkSeguro(conn, _schema, _tablaUsuario, "USUCOD", codigo, "USUCO5");
                    }

                    decimal? codigoFinanciero = null;
                    if (!string.IsNullOrWhiteSpace(pkAdicional) && columnasAdicional.Contains("USUOID"))
                    {
                        var valorCrudo = ObtenerCampoPorPkSeguro(conn, _schema, _tablaAdicional, pkAdicional, codigo, "USUOID");
                        decimal valorNumerico;
                        if (decimal.TryParse(valorCrudo, NumberStyles.Any, CultureInfo.InvariantCulture, out valorNumerico)
                            || decimal.TryParse(valorCrudo, NumberStyles.Any, CultureInfo.CurrentCulture, out valorNumerico))
                        {
                            if (valorNumerico > 0m)
                            {
                                codigoFinanciero = valorNumerico;
                            }
                        }
                    }

                    return new UsuarioInternoAs400Info
                    {
                        CodigoUsuario = codigo,
                        CiudadCodigo = string.IsNullOrWhiteSpace(ciudad)
                            ? string.Empty
                            : ciudad.Trim().ToUpperInvariant(),
                        CodigoFinanciero = codigoFinanciero,
                        Opcoi3 = codigoFinanciero
                    };
                });
            }
            catch
            {
                return null;
            }
        }

        private string ResolverCodigoUsuarioInterno(OdbcConnection conn, string codigoOIdentificacion)
        {
            if (string.IsNullOrWhiteSpace(codigoOIdentificacion))
            {
                return null;
            }

            var valor = SafeString(codigoOIdentificacion, 10).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(valor))
            {
                return null;
            }

            var codigo = ObtenerCampoPorPkSeguro(conn, _schema, _tablaUsuario, "USUCOD", valor, "USUCOD");
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                return codigo.Trim().ToUpperInvariant();
            }

            codigo = ObtenerCampoPorPkSeguro(conn, _schema, _tablaAdicional, "USUCO8", valor, "USUCO8");
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                return codigo.Trim().ToUpperInvariant();
            }

            codigo = ObtenerCampoPorPkSeguro(conn, _schema, _tablaAdicional, "USUCOD", valor, "USUCOD");
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                return codigo.Trim().ToUpperInvariant();
            }

            var columnasUsuario = GetColumnas(conn, _schema, _tablaUsuario);
            if (columnasUsuario.Contains("USUCED"))
            {
                codigo = ObtenerCampoPorFiltroSeguro(conn, _schema, _tablaUsuario, "USUCED", valor, "USUCOD");
                if (!string.IsNullOrWhiteSpace(codigo))
                {
                    return codigo.Trim().ToUpperInvariant();
                }
            }

            var columnasAdicional = GetColumnas(conn, _schema, _tablaAdicional);
            if (columnasAdicional.Contains("USUCED"))
            {
                if (columnasAdicional.Contains("USUCO8"))
                {
                    codigo = ObtenerCampoPorFiltroSeguro(conn, _schema, _tablaAdicional, "USUCED", valor, "USUCO8");
                    if (!string.IsNullOrWhiteSpace(codigo))
                    {
                        return codigo.Trim().ToUpperInvariant();
                    }
                }

                if (columnasAdicional.Contains("USUCOD"))
                {
                    codigo = ObtenerCampoPorFiltroSeguro(conn, _schema, _tablaAdicional, "USUCED", valor, "USUCOD");
                    if (!string.IsNullOrWhiteSpace(codigo))
                    {
                        return codigo.Trim().ToUpperInvariant();
                    }
                }
            }

            return null;
        }

        public string ObtenerNumeroRucPorCodigoUsuario(string codigoUsuario)
        {
            if (string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return null;
            }

            var codigo = SafeString(codigoUsuario, 10).ToUpperInvariant();

            try
            {
                return ExecuteWithConnection(conn =>
                {
                    // Ruta directa (más robusta): evita depender de metadatos para lectura.
                    var numero = ObtenerCampoPorPkSeguro(conn, _schema, _tablaUsuario, "USUCOD", codigo, "USUNUM");
                    if (!string.IsNullOrWhiteSpace(numero))
                    {
                        return numero;
                    }

                    numero = ObtenerCampoPorPkSeguro(conn, _schema, _tablaAdicional, "USUCO8", codigo, "USUNUM");
                    if (!string.IsNullOrWhiteSpace(numero))
                    {
                        return numero;
                    }

                    numero = ObtenerCampoPorPkSeguro(conn, _schema, _tablaAdicional, "USUCOD", codigo, "USUNUM");
                    if (!string.IsNullOrWhiteSpace(numero))
                    {
                        return numero;
                    }

                    // Fallback legacy por columnas detectadas.
                    var columnasUsuario = GetColumnas(conn, _schema, _tablaUsuario);
                    if (columnasUsuario.Contains("USUNUM"))
                    {
                        numero = ObtenerCampoPorPk(conn, _schema, _tablaUsuario, "USUCOD", codigo, "USUNUM");
                        if (!string.IsNullOrWhiteSpace(numero))
                        {
                            return numero;
                        }
                    }

                    var columnasAdicional = GetColumnas(conn, _schema, _tablaAdicional);
                    var pkAdicional = columnasAdicional.Contains("USUCO8")
                        ? "USUCO8"
                        : (columnasAdicional.Contains("USUCOD") ? "USUCOD" : null);

                    if (!string.IsNullOrWhiteSpace(pkAdicional) && columnasAdicional.Contains("USUNUM"))
                    {
                        var numeroAdicional = ObtenerCampoPorPk(
                            conn,
                            _schema,
                            _tablaAdicional,
                            pkAdicional,
                            codigo,
                            "USUNUM");
                        if (!string.IsNullOrWhiteSpace(numeroAdicional))
                        {
                            return numeroAdicional;
                        }
                    }

                    return null;
                });
            }
            catch
            {
                return null;
            }
        }

        public string ObtenerCedulaPorCodigoUsuario(string codigoUsuario)
        {
            if (string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return null;
            }

            var codigo = SafeString(codigoUsuario, 10).ToUpperInvariant();

            try
            {
                return ExecuteWithConnection(conn =>
                {
                    // Ruta directa (más robusta): evita depender de metadatos para lectura.
                    var cedula = ObtenerCampoPorPkSeguro(conn, _schema, _tablaUsuario, "USUCOD", codigo, "USUCED");
                    if (!string.IsNullOrWhiteSpace(cedula))
                    {
                        return cedula;
                    }

                    cedula = ObtenerCampoPorPkSeguro(conn, _schema, _tablaAdicional, "USUCO8", codigo, "USUCED");
                    if (!string.IsNullOrWhiteSpace(cedula))
                    {
                        return cedula;
                    }

                    cedula = ObtenerCampoPorPkSeguro(conn, _schema, _tablaAdicional, "USUCOD", codigo, "USUCED");
                    if (!string.IsNullOrWhiteSpace(cedula))
                    {
                        return cedula;
                    }

                    // Fallback legacy por columnas detectadas.
                    var columnasUsuario = GetColumnas(conn, _schema, _tablaUsuario);
                    if (columnasUsuario.Contains("USUCED"))
                    {
                        cedula = ObtenerCampoPorPk(conn, _schema, _tablaUsuario, "USUCOD", codigo, "USUCED");
                        if (!string.IsNullOrWhiteSpace(cedula))
                        {
                            return cedula;
                        }
                    }

                    var columnasAdicional = GetColumnas(conn, _schema, _tablaAdicional);
                    var pkAdicional = columnasAdicional.Contains("USUCO8")
                        ? "USUCO8"
                        : (columnasAdicional.Contains("USUCOD") ? "USUCOD" : null);

                    if (!string.IsNullOrWhiteSpace(pkAdicional) && columnasAdicional.Contains("USUCED"))
                    {
                        var cedulaAdicional = ObtenerCampoPorPk(
                            conn,
                            _schema,
                            _tablaAdicional,
                            pkAdicional,
                            codigo,
                            "USUCED");
                        if (!string.IsNullOrWhiteSpace(cedulaAdicional))
                        {
                            return cedulaAdicional;
                        }
                    }

                    return null;
                });
            }
            catch
            {
                return null;
            }
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

        private string ObtenerCampoPorPk(
            OdbcConnection conn,
            string schema,
            string table,
            string pkColumn,
            string pkValue,
            string campo)
        {
            var sql = $"SELECT TRIM({campo}) FROM {schema}.{table} WHERE {pkColumn} = ? FETCH FIRST 1 ROWS ONLY";
            using (var cmd = new OdbcCommand(sql, conn))
            {
                AddParameter(cmd, pkValue, OdbcType.VarChar);
                var value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return null;
                }

                var texto = value.ToString();
                return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
            }
        }

        private string ObtenerCampoPorFiltro(
            OdbcConnection conn,
            string schema,
            string table,
            string filtroColumn,
            string filtroValue,
            string campo)
        {
            var sql = $"SELECT TRIM({campo}) FROM {schema}.{table} WHERE {filtroColumn} = ? FETCH FIRST 1 ROWS ONLY";
            using (var cmd = new OdbcCommand(sql, conn))
            {
                AddParameter(cmd, filtroValue, OdbcType.VarChar);
                var value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return null;
                }

                var texto = value.ToString();
                return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
            }
        }

        private string ObtenerCampoPorPkSeguro(
            OdbcConnection conn,
            string schema,
            string table,
            string pkColumn,
            string pkValue,
            string campo)
        {
            try
            {
                return ObtenerCampoPorPk(conn, schema, table, pkColumn, pkValue, campo);
            }
            catch
            {
                return null;
            }
        }

        private string ObtenerCampoPorFiltroSeguro(
            OdbcConnection conn,
            string schema,
            string table,
            string filtroColumn,
            string filtroValue,
            string campo)
        {
            try
            {
                return ObtenerCampoPorFiltro(conn, schema, table, filtroColumn, filtroValue, campo);
            }
            catch
            {
                return null;
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
