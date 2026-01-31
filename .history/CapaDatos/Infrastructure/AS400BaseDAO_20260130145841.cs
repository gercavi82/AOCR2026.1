using System;
using System.Data;
using System.Data.Odbc;
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
        protected readonly int _commandTimeout;
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

            var creds = configService.GetAS400Credentials();
            _connectionString = BuildConnectionString(creds);
            _commandTimeout = commandTimeout;
        }

        private string BuildConnectionString(AS400Credentials creds)
        {
            if (string.IsNullOrWhiteSpace(creds.Server))
            {
                throw new InvalidOperationException("Servidor AS400 no configurado.");
            }

            // Connection string para IBM i Access ODBC
            return string.Format(
                "Driver={{IBM i Access ODBC Driver}};System={0};Database={1};Uid={2};Pwd={3};" +
                "Connection Timeout={4};Query Timeout={5};",
                creds.Server,
                creds.Database ?? creds.Library,
                creds.UserId,
                creds.Password,
                DefaultConnectionTimeout,
                _commandTimeout);
        }

        #endregion

        #region Métodos de Conexión

        protected OdbcConnection CreateConnection()
        {
            return new OdbcConnection(_connectionString);
        }

        protected T ExecuteWithConnection<T>(Func<OdbcConnection, T> action)
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
        }

        protected void ExecuteWithConnection(Action<OdbcConnection> action)
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
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }
            return reader.GetString(ordinal).TrimEnd();
        }

        protected decimal GetDecimal(IDataReader reader, int ordinal, decimal defaultValue = 0m)
        {
            if (reader.IsDBNull(ordinal))
            {
                return defaultValue;
            }
            return reader.GetDecimal(ordinal);
        }

        protected int GetInt(IDataReader reader, int ordinal, int defaultValue = 0)
        {
            if (reader.IsDBNull(ordinal))
            {
                return defaultValue;
            }
            return reader.GetInt32(ordinal);
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
