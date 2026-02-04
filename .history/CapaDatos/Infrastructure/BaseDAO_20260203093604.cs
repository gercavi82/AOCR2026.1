using System;
using System.Data;
using System.Data.Common;
using Npgsql;

namespace CapaDatos.Infrastructure
{
    /// <summary>
    /// Clase base para todos los DAOs con manejo seguro de conexiones y transacciones.
    /// </summary>
    public abstract class BaseDAO : IDisposable
    {
        #region Campos

        protected readonly string _connectionString;
        protected readonly int _commandTimeout;
        private bool _disposed;

        #endregion

        #region Configuración

        private const int DefaultCommandTimeout = 30; // segundos
        private const int DefaultConnectionTimeout = 15; // segundos

        #endregion

        #region Constructor

        protected BaseDAO(string connectionString, int commandTimeout = DefaultCommandTimeout)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }

            _connectionString = connectionString;
            _commandTimeout = commandTimeout;
        }

        #endregion

        #region Métodos de Conexión

        /// <summary>
        /// Crea una nueva conexión PostgreSQL
        /// </summary>
        protected NpgsqlConnection CreateConnection()
        {
            var conn = new NpgsqlConnection(_connectionString);
            return conn;
        }

        /// <summary>
        /// Ejecuta una acción con conexión manejada automáticamente
        /// </summary>
        protected T ExecuteWithConnection<T>(Func<NpgsqlConnection, T> action)
        {
            using (var connection = CreateConnection())
            {
                try
                {
                    connection.Open();
                    return action(connection);
                }
                catch (NpgsqlException ex)
                {
                    throw WrapDatabaseException(ex);
                }
            }
        }

        /// <summary>
        /// Ejecuta una acción con conexión (sin retorno)
        /// </summary>
        protected void ExecuteWithConnection(Action<NpgsqlConnection> action)
        {
            using (var connection = CreateConnection())
            {
                try
                {
                    connection.Open();
                    action(connection);
                }
                catch (NpgsqlException ex)
                {
                    throw WrapDatabaseException(ex);
                }
            }
        }

        /// <summary>
        /// Ejecuta una acción dentro de una transacción
        /// </summary>
        protected T ExecuteInTransaction<T>(Func<NpgsqlConnection, NpgsqlTransaction, T> action, 
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(isolationLevel))
                {
                    try
                    {
                        var result = action(connection, transaction);
                        transaction.Commit();
                        return result;
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception rollbackEx)
                        {
                            System.Diagnostics.Debug.WriteLine("Error en rollback: " + rollbackEx.Message);
                        }

                        if (ex is NpgsqlException)
                        {
                            throw WrapDatabaseException((NpgsqlException)ex);
                        }
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Ejecuta una acción dentro de una transacción (sin retorno)
        /// </summary>
        protected void ExecuteInTransaction(Action<NpgsqlConnection, NpgsqlTransaction> action,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            ExecuteInTransaction<object>((conn, trans) =>
            {
                action(conn, trans);
                return null;
            }, isolationLevel);
        }

        #endregion

        #region Métodos de Comando

        /// <summary>
        /// Crea un comando con parámetros de forma segura
        /// </summary>
        protected NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql, 
            NpgsqlTransaction transaction = null)
        {
            var cmd = new NpgsqlCommand(sql, connection);
            cmd.CommandTimeout = _commandTimeout;

            if (transaction != null)
            {
                cmd.Transaction = transaction;
            }

            return cmd;
        }

        /// <summary>
        /// Agrega un parámetro de forma segura (previene SQL injection)
        /// </summary>
        protected void AddParameter(NpgsqlCommand cmd, string name, object value, NpgsqlTypes.NpgsqlDbType dbType)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.NpgsqlDbType = dbType;
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }

        /// <summary>
        /// Agrega un parámetro con inferencia de tipo
        /// </summary>
        protected void AddParameter(NpgsqlCommand cmd, string name, object value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }

        /// <summary>
        /// Ejecuta un comando escalar de forma segura
        /// </summary>
        protected T ExecuteScalar<T>(NpgsqlConnection connection, string sql, 
            Action<NpgsqlCommand> addParameters = null, NpgsqlTransaction transaction = null)
        {
            using (var cmd = CreateCommand(connection, sql, transaction))
            {
                if (addParameters != null)
                {
                    addParameters(cmd);
                }

                var result = cmd.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    return default(T);
                }

                return (T)Convert.ChangeType(result, typeof(T));
            }
        }

        /// <summary>
        /// Ejecuta un comando no-query de forma segura
        /// </summary>
        protected int ExecuteNonQuery(NpgsqlConnection connection, string sql,
            Action<NpgsqlCommand> addParameters = null, NpgsqlTransaction transaction = null)
        {
            using (var cmd = CreateCommand(connection, sql, transaction))
            {
                if (addParameters != null)
                {
                    addParameters(cmd);
                }

                return cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Manejo de Errores

        /// <summary>
        /// Envuelve excepciones de base de datos para no filtrar detalles internos
        /// </summary>
        protected DataAccessException WrapDatabaseException(NpgsqlException ex)
        {
            // Log detallado interno
            System.Diagnostics.Debug.WriteLine("Error de BD: " + ex.Message);
            
            // Intentar obtener código de error de forma compatible
            string sqlState = null;
            try
            {
                // Npgsql 4.x usa ex.Data["SqlState"] o ex.ErrorCode
                if (ex.Data.Contains("SqlState"))
                {
                    sqlState = ex.Data["SqlState"] as string;
                }
            }
            catch (Exception innerEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo SqlState: {innerEx.Message}");
                // Continuar sin SqlState - usaremos clasificación por mensaje
            }

            System.Diagnostics.Debug.WriteLine("SqlState: " + (sqlState ?? "N/A"));

            // Mapear códigos de error PostgreSQL a mensajes amigables
            string userMessage;
            string errorCode;

            // Si no pudimos obtener SqlState, usar el mensaje para clasificar
            if (string.IsNullOrEmpty(sqlState))
            {
                var msgLower = ex.Message.ToLowerInvariant();
                
                if (msgLower.Contains("duplicate") || msgLower.Contains("unique"))
                {
                    userMessage = "Ya existe un registro con los mismos datos únicos.";
                    errorCode = "DUPLICATE_KEY";
                }
                else if (msgLower.Contains("foreign key") || msgLower.Contains("referential"))
                {
                    userMessage = "No se puede completar la operación porque hay datos relacionados.";
                    errorCode = "FOREIGN_KEY";
                }
                else if (msgLower.Contains("null") && msgLower.Contains("violat"))
                {
                    userMessage = "Faltan datos obligatorios.";
                    errorCode = "NULL_VIOLATION";
                }
                else if (msgLower.Contains("connection") || msgLower.Contains("timeout"))
                {
                    userMessage = "No se puede conectar a la base de datos. Intente más tarde.";
                    errorCode = "CONNECTION_ERROR";
                }
                else if (msgLower.Contains("deadlock") || msgLower.Contains("serializ"))
                {
                    userMessage = "Conflicto de concurrencia. Por favor, intente nuevamente.";
                    errorCode = "CONCURRENCY_ERROR";
                }
                else
                {
                    userMessage = "Error al procesar la solicitud. Contacte al administrador.";
                    errorCode = "DATABASE_ERROR";
                }
            }
            else
            {
                // Usar SqlState si está disponible
                switch (sqlState)
                {
                    case "23505":
                        userMessage = "Ya existe un registro con los mismos datos únicos.";
                        errorCode = "DUPLICATE_KEY";
                        break;
                    case "23503":
                        userMessage = "No se puede completar la operación porque hay datos relacionados.";
                        errorCode = "FOREIGN_KEY";
                        break;
                    case "23502":
                        userMessage = "Faltan datos obligatorios.";
                        errorCode = "NULL_VIOLATION";
                        break;
                    case "42P01":
                    case "42703":
                        userMessage = "Error de configuración de base de datos.";
                        errorCode = "SCHEMA_ERROR";
                        break;
                    case "08000":
                    case "08003":
                    case "08006":
                        userMessage = "No se puede conectar a la base de datos. Intente más tarde.";
                        errorCode = "CONNECTION_ERROR";
                        break;
                    case "40001":
                    case "40P01":
                        userMessage = "Conflicto de concurrencia. Por favor, intente nuevamente.";
                        errorCode = "CONCURRENCY_ERROR";
                        break;
                    default:
                        userMessage = "Error al procesar la solicitud. Contacte al administrador.";
                        errorCode = "DATABASE_ERROR";
                        break;
                }
            }

            return new DataAccessException(userMessage, errorCode, ex);
        }

        #endregion

        #region Helpers de Lectura

        /// <summary>
        /// Lee un valor de forma segura desde un DataReader
        /// </summary>
        protected T GetValue<T>(IDataReader reader, string columnName, T defaultValue = default(T))
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal))
                {
                    return defaultValue;
                }

                var value = reader.GetValue(ordinal);
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Lee un string de forma segura
        /// </summary>
        protected string GetString(IDataReader reader, string columnName)
        {
            return GetValue<string>(reader, columnName, null);
        }

        /// <summary>
        /// Lee un int de forma segura
        /// </summary>
        protected int GetInt(IDataReader reader, string columnName, int defaultValue = 0)
        {
            return GetValue<int>(reader, columnName, defaultValue);
        }

        /// <summary>
        /// Lee un decimal de forma segura
        /// </summary>
        protected decimal GetDecimal(IDataReader reader, string columnName, decimal defaultValue = 0m)
        {
            return GetValue<decimal>(reader, columnName, defaultValue);
        }

        /// <summary>
        /// Lee un DateTime de forma segura
        /// </summary>
        protected DateTime GetDateTime(IDataReader reader, string columnName, DateTime? defaultValue = null)
        {
            return GetValue<DateTime>(reader, columnName, defaultValue ?? DateTime.MinValue);
        }

        /// <summary>
        /// Lee un DateTime nullable de forma segura
        /// </summary>
        protected DateTime? GetNullableDateTime(IDataReader reader, string columnName)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal))
                {
                    return null;
                }
                return reader.GetDateTime(ordinal);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Lee un bool de forma segura
        /// </summary>
        protected bool GetBool(IDataReader reader, string columnName, bool defaultValue = false)
        {
            return GetValue<bool>(reader, columnName, defaultValue);
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
                if (disposing)
                {
                    // Liberar recursos manejados
                }
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Excepción personalizada para errores de acceso a datos
    /// </summary>
    public class DataAccessException : Exception
    {
        public string ErrorCode { get; private set; }

        public DataAccessException(string message, string errorCode, Exception innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}
