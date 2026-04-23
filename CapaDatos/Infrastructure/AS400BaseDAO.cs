using System;
using System.Data;
using System.Data.Odbc;
using System.Globalization;
using CapaDatos.Services;

namespace CapaDatos.Infrastructure
{
    /// <summary>
    /// Clase base para DAOs que acceden a DB2/AS400.
    /// Maneja conexiones ODBC con timeouts y pooling apropiados.
    /// </summary>
    public abstract class AS400BaseDAO : IDisposable
    {
        #region Campos

        protected readonly string _connectionString;
        protected readonly string _fallbackConnectionString;
        protected readonly int _commandTimeout;
        protected readonly ISecureConfigurationService _configService;
        protected readonly CircuitBreaker _circuitBreaker;
        private bool _disposed;

        #endregion

        #region Configuración

        private const int DefaultCommandTimeout = 60; // AS400 puede ser más lento
        private const int DefaultConnectionTimeout = 30;

        #endregion

        #region Constructor

        protected AS400BaseDAO(ISecureConfigurationService configService, int commandTimeout = DefaultCommandTimeout)
        {
            if (configService == null)
            {
                throw new ArgumentNullException("configService");
            }

            _configService = configService;
            _commandTimeout = commandTimeout > 0 ? commandTimeout : DefaultCommandTimeout;
            _circuitBreaker = CircuitBreakerRegistry.GetOrCreate("AS400");

            var creds = configService.GetAS400Credentials();
            _connectionString = BuildConnectionString(creds);
            _fallbackConnectionString = BuildAlternativeConnectionString(creds);
        }

        private string BuildConnectionString(AS400Credentials creds)
        {
            var dsn = SafeTrim(_configService.GetAppSetting("AS400:Dsn"));
            if (!string.IsNullOrWhiteSpace(dsn))
            {
                var dsnBuilder = new OdbcConnectionStringBuilder();
                dsnBuilder["Dsn"] = dsn;
                if (!string.IsNullOrWhiteSpace(creds.UserId))
                {
                    dsnBuilder["Uid"] = creds.UserId;
                }
                if (!string.IsNullOrWhiteSpace(creds.Password))
                {
                    dsnBuilder["Pwd"] = creds.Password;
                }
                dsnBuilder["Connection Timeout"] = DefaultConnectionTimeout.ToString(CultureInfo.InvariantCulture);
                dsnBuilder["Query Timeout"] = _commandTimeout.ToString(CultureInfo.InvariantCulture);
                return dsnBuilder.ConnectionString;
            }

            if (string.IsNullOrWhiteSpace(creds.Server))
            {
                throw new InvalidOperationException("Servidor AS400 no configurado.");
            }

            var driver = SafeTrim(_configService.GetAppSetting("AS400:OdbcDriver"));
            if (string.IsNullOrWhiteSpace(driver))
            {
                driver = "iSeries Access ODBC Driver";
            }

            var builder = new OdbcConnectionStringBuilder();
            builder.Driver = driver;
            builder["System"] = creds.Server;

            var database = !string.IsNullOrWhiteSpace(creds.Library)
                ? creds.Library
                : creds.Database;
            if (!string.IsNullOrWhiteSpace(database))
            {
                builder["Database"] = database;
                builder["DefaultCollection"] = database;
            }

            if (!string.IsNullOrWhiteSpace(creds.UserId))
            {
                builder["Uid"] = creds.UserId;
            }
            if (!string.IsNullOrWhiteSpace(creds.Password))
            {
                builder["Pwd"] = creds.Password;
            }

            builder["Connection Timeout"] = DefaultConnectionTimeout.ToString(CultureInfo.InvariantCulture);
            builder["Query Timeout"] = _commandTimeout.ToString(CultureInfo.InvariantCulture);
            return builder.ConnectionString;
        }

        /// <summary>
        /// Obtiene cadena de conexión alternativa usando Client Access ODBC Driver
        /// </summary>
        private string BuildAlternativeConnectionString(AS400Credentials creds)
        {
            if (string.IsNullOrWhiteSpace(creds.Server))
            {
                return null;
            }

            var builder = new OdbcConnectionStringBuilder();
            builder.Driver = "Client Access ODBC Driver (32-bit)";
            builder["System"] = creds.Server;

            var database = !string.IsNullOrWhiteSpace(creds.Library)
                ? creds.Library
                : creds.Database;
            if (!string.IsNullOrWhiteSpace(database))
            {
                builder["Database"] = database;
                builder["DefaultCollection"] = database;
            }

            if (!string.IsNullOrWhiteSpace(creds.UserId))
            {
                builder["Uid"] = creds.UserId;
            }
            if (!string.IsNullOrWhiteSpace(creds.Password))
            {
                builder["Pwd"] = creds.Password;
            }

            builder["Connection Timeout"] = DefaultConnectionTimeout.ToString(CultureInfo.InvariantCulture);
            builder["Query Timeout"] = _commandTimeout.ToString(CultureInfo.InvariantCulture);
            return builder.ConnectionString;
        }

        #endregion

        #region Métodos de Conexión

        protected OdbcConnection CreateConnection()
        {
            if (CanOpen(_connectionString))
            {
                return new OdbcConnection(_connectionString);
            }

            if (!string.IsNullOrWhiteSpace(_fallbackConnectionString) && CanOpen(_fallbackConnectionString))
            {
                return new OdbcConnection(_fallbackConnectionString);
            }

            // Si no se pudo probar, devolver la conexión principal para propagar el error real al abrir.
            return new OdbcConnection(_connectionString);
        }

        protected T ExecuteWithConnection<T>(Func<OdbcConnection, T> action)
        {
            return _circuitBreaker.Execute(() =>
            {
                using (var connection = CreateConnection())
                {
                    try
                    {
                        connection.Open();
                        return action(connection);
                    }
                    catch (OdbcException ex)
                    {
                        throw WrapAS400Exception(ex);
                    }
                }
            });
        }

        protected void ExecuteWithConnection(Action<OdbcConnection> action)
        {
            _circuitBreaker.Execute(() =>
            {
                using (var connection = CreateConnection())
                {
                    try
                    {
                        connection.Open();
                        action(connection);
                    }
                    catch (OdbcException ex)
                    {
                        throw WrapAS400Exception(ex);
                    }
                }
                return 0; // dummy return for Func<T>
            });
        }

        protected bool TryTestConnection(out string message)
        {
            message = null;
            try
            {
                ExecuteWithConnection(conn =>
                {
                    using (var cmd = CreateCommand(conn, "SELECT 1 FROM SYSIBM.SYSDUMMY1"))
                    {
                        var scalar = cmd.ExecuteScalar();
                        if (scalar == null || scalar == DBNull.Value)
                        {
                            throw new InvalidOperationException("DB2 devolvió una respuesta vacía.");
                        }
                    }
                });

                message = "OK";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static string SafeTrim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private bool CanOpen(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            try
            {
                using (var connection = new OdbcConnection(connectionString))
                {
                    connection.Open();
                    connection.Close();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[AS400BaseDAO] CanOpen fallo: " + ex.GetType().FullName + " - " + ex.Message);
                return false;
            }
        }

        #endregion

        #region Métodos de Comando

        protected OdbcCommand CreateCommand(OdbcConnection connection, string sql)
        {
            var cmd = new OdbcCommand(sql, connection);
            cmd.CommandTimeout = _commandTimeout;
            return cmd;
        }

        /// <summary>
        /// Agrega parámetro ODBC (usa ? como placeholder en AS400)
        /// </summary>
        protected void AddParameter(OdbcCommand cmd, object value, OdbcType dbType)
        {
            var param = cmd.CreateParameter();
            param.OdbcType = dbType;
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }

        #endregion

        #region Manejo de Errores

        protected DataAccessException WrapAS400Exception(OdbcException ex)
        {
            System.Diagnostics.Debug.WriteLine("Error AS400: " + ex.Message);
            System.Diagnostics.Debug.WriteLine("Estado: " + ex.Source);

            string userMessage;
            string errorCode;

            // Mapear errores comunes de AS400
            var errorMsg = ex.Message.ToUpperInvariant();

            if (errorMsg.Contains("COMMUNICATION") || errorMsg.Contains("CONNECTION"))
            {
                userMessage = "No se puede conectar al sistema AS400. Intente más tarde.";
                errorCode = "AS400_CONNECTION";
            }
            else if (errorMsg.Contains("AUTHORITY") || errorMsg.Contains("PERMISSION"))
            {
                userMessage = "No tiene permisos para acceder a estos datos.";
                errorCode = "AS400_PERMISSION";
            }
            else if (errorMsg.Contains("NOT FOUND") || errorMsg.Contains("DOES NOT EXIST"))
            {
                userMessage = "El recurso solicitado no existe en el sistema.";
                errorCode = "AS400_NOT_FOUND";
            }
            else
            {
                userMessage = "Error al consultar el sistema AS400. Contacte al administrador.";
                errorCode = "AS400_ERROR";
            }

            return new DataAccessException(userMessage, errorCode, ex);
        }

        #endregion

        #region Helpers de Lectura

        protected string GetString(IDataReader reader, int ordinal)
        {
            if (reader == null)
            {
                return null;
            }

            try
            {
                if (ordinal < 0 || ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
                {
                    return null;
                }

                var value = reader.GetValue(ordinal);
                if (value == null || value == DBNull.Value)
                {
                    return null;
                }

                var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[AS400BaseDAO] GetString fallo: ordinal=" + ordinal + ", error=" + ex.GetType().FullName + ", msg=" + ex.Message);
                return null;
            }
        }

        protected decimal GetDecimal(IDataReader reader, int ordinal, decimal defaultValue = 0m)
        {
            if (reader == null)
            {
                return defaultValue;
            }

            try
            {
                if (ordinal < 0 || ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
                {
                    return defaultValue;
                }

                var value = reader.GetValue(ordinal);
                if (value == null || value == DBNull.Value)
                {
                    return defaultValue;
                }

                if (value is decimal decimalValue) return decimalValue;
                if (value is int intValue) return intValue;
                if (value is long longValue) return longValue;
                if (value is short shortValue) return shortValue;
                if (value is double doubleValue) return (decimal)doubleValue;
                if (value is float floatValue) return (decimal)floatValue;

                decimal parsed;
                if (decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)
                    || decimal.TryParse(Convert.ToString(value, CultureInfo.CurrentCulture), NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
                {
                    return parsed;
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[AS400BaseDAO] GetDecimal fallo: ordinal=" + ordinal + ", error=" + ex.GetType().FullName + ", msg=" + ex.Message);
                return defaultValue;
            }
        }

        protected int GetInt(IDataReader reader, int ordinal, int defaultValue = 0)
        {
            if (reader == null)
            {
                return defaultValue;
            }

            try
            {
                if (ordinal < 0 || ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
                {
                    return defaultValue;
                }

                var value = reader.GetValue(ordinal);
                if (value == null || value == DBNull.Value)
                {
                    return defaultValue;
                }

                if (value is int intValue) return intValue;
                if (value is long longValue) return Convert.ToInt32(longValue);
                if (value is short shortValue) return shortValue;
                if (value is decimal decimalValue) return Convert.ToInt32(decimalValue);

                int parsed;
                return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)
                    ? parsed
                    : defaultValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[AS400BaseDAO] GetInt fallo: ordinal=" + ordinal + ", error=" + ex.GetType().FullName + ", msg=" + ex.Message);
                return defaultValue;
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        #endregion
    }
}
