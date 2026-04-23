using System;
using System.Data;
using System.Globalization;
using System.Text;
using IBM.Data.DB2.iSeries;
using CapaDatos.Models;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    public class CD_UbicacionUsuario
    {
        private static CD_UbicacionUsuario _instancia;
        private readonly string _connectionString;

        private CD_UbicacionUsuario()
        {
            var configService = new SecureConfigurationService();
            var creds = configService.GetAS400Credentials();
            _connectionString = BuildConnectionString(creds);
        }

        public static CD_UbicacionUsuario Instancia
        {
            get
            {
                if (_instancia == null)
                {
                    _instancia = new CD_UbicacionUsuario();
                }

                return _instancia;
            }
        }

        public UbicacionUsuarioRecord UbicacionUsuarioPorCiudad(string codCiudad)
        {
            if (string.IsNullOrWhiteSpace(codCiudad))
            {
                return null;
            }

            const string query = @"
                SELECT
                    COALESCE(TRIM(CHAR(OPUOID)), '0') AS OidUbicacion,
                    COALESCE(TRIM(OPUEST), '') AS Estacion,
                    COALESCE(TRIM(OPUCOD), '') AS CodigoCiudad
                FROM OPUARC01
                WHERE TRIM(OPUCOD) = @codCiudad
                FETCH FIRST 1 ROW ONLY";

            const string rescueQuery = @"
                SELECT
                    OPUEST AS Estacion,
                    OPUCOD AS CodigoCiudad
                FROM OPUARC01
                WHERE TRIM(OPUCOD) = @codCiudad
                FETCH FIRST 1 ROW ONLY";

            return EjecutarConsultaUbicacion("OPUARC01", query, codCiudad, rescueQuery);
        }

        public UbicacionUsuarioRecord UbicacionAeropuertoUsuarioPorCiudad(string codCiudad)
        {
            if (string.IsNullOrWhiteSpace(codCiudad))
            {
                return null;
            }

            const string query = @"
                SELECT
                    COALESCE(TRIM(CHAR(OIDOI2)), '0') AS OidUbicacion,
                    COALESCE(TRIM(OIDNO2), '') AS Estacion,
                    COALESCE(TRIM(OIDCO3), '') AS CodigoCiudad
                FROM OIDAR2
                WHERE TRIM(OIDCO3) = @codCiudad
                FETCH FIRST 1 ROW ONLY";

            const string rescueQuery = @"
                SELECT
                    OIDNO2 AS Estacion,
                    OIDCO3 AS CodigoCiudad
                FROM OIDAR2
                WHERE TRIM(OIDCO3) = @codCiudad
                FETCH FIRST 1 ROW ONLY";

            return EjecutarConsultaUbicacion("OIDAR2", query, codCiudad, rescueQuery);
        }

        private UbicacionUsuarioRecord EjecutarConsultaUbicacion(
            string origen,
            string query,
            string codCiudad,
            string rescueQuery)
        {
            var codCiudadNormalizado = codCiudad.Trim().ToUpperInvariant();
            iDB2Connection conexion = null;
            iDB2Command cmd = null;
            iDB2DataReader dr = null;

            try
            {
                conexion = new iDB2Connection(_connectionString);
                cmd = new iDB2Command(query, conexion);
                cmd.Parameters.Add("@codCiudad", iDB2DbType.iDB2VarChar).Value = codCiudadNormalizado;
                conexion.Open();

                dr = cmd.ExecuteReader();
                if (dr == null || !dr.Read())
                {
                    return null;
                }

                return new UbicacionUsuarioRecord
                {
                    OidUbicacion = SafeReadDecimal(dr, "OidUbicacion", origen, codCiudadNormalizado),
                    Estacion = SafeReadString(dr, "Estacion", origen, codCiudadNormalizado),
                    CodigoCiudad = SafeReadString(dr, "CodigoCiudad", origen, codCiudadNormalizado)
                };
            }
            catch (iDB2ConversionException ex)
            {
                LogIssue(
                    "DB2_CONVERSION",
                    origen,
                    codCiudadNormalizado,
                    "N/A",
                    null,
                    "Conversión DB2 fallida en lectura principal. Se intentará consulta de rescate solo-estación.",
                    ex);
                return EjecutarConsultaRescateSoloEstacion(origen, rescueQuery, codCiudadNormalizado);
            }
            catch (Exception ex)
            {
                LogIssue(
                    "DB2_ERROR",
                    origen,
                    codCiudadNormalizado,
                    "N/A",
                    null,
                    "Error inesperado consultando ubicación en AS400. Se aplicará fallback del flujo.",
                    ex);
                return EjecutarConsultaRescateSoloEstacion(origen, rescueQuery, codCiudadNormalizado);
            }
                finally
                {
                    if (dr != null)
                    {
                        try { dr.Close(); } catch { }
                        try { dr.Dispose(); } catch { }
                    }

                    if (cmd != null)
                    {
                        try { cmd.Dispose(); } catch { }
                    }

                    SafeDisposeConnection(conexion);
                }
        }

        private UbicacionUsuarioRecord EjecutarConsultaRescateSoloEstacion(
            string origen,
            string rescueQuery,
            string codCiudadNormalizado)
        {
            if (string.IsNullOrWhiteSpace(rescueQuery))
            {
                return null;
            }

            iDB2Connection conexion = null;
            iDB2Command cmd = null;
            iDB2DataReader dr = null;

            try
            {
                conexion = new iDB2Connection(_connectionString);
                cmd = new iDB2Command(rescueQuery, conexion);
                cmd.Parameters.Add("@codCiudad", iDB2DbType.iDB2VarChar).Value = codCiudadNormalizado;
                conexion.Open();

                dr = cmd.ExecuteReader();
                if (dr == null || !dr.Read())
                {
                    LogIssue(
                        "DB2_RESCUE_EMPTY",
                        origen,
                        codCiudadNormalizado,
                        "Estacion",
                        null,
                        "Consulta de rescate no devolvió filas.",
                        null);
                    return null;
                }

                return new UbicacionUsuarioRecord
                {
                    OidUbicacion = 0m,
                    Estacion = SafeReadString(dr, "Estacion", origen + "_RESCUE", codCiudadNormalizado),
                    CodigoCiudad = SafeReadString(dr, "CodigoCiudad", origen + "_RESCUE", codCiudadNormalizado)
                };
            }
            catch (Exception ex)
            {
                LogIssue(
                    "DB2_RESCUE_ERROR",
                    origen,
                    codCiudadNormalizado,
                    "Estacion",
                    null,
                    "Consulta de rescate falló.",
                    ex);
                return null;
            }
            finally
            {
                if (dr != null)
                {
                    try { dr.Close(); } catch { }
                    try { dr.Dispose(); } catch { }
                }

                if (cmd != null)
                {
                    try { cmd.Dispose(); } catch { }
                }

                SafeDisposeConnection(conexion);
            }
        }

        private static void SafeDisposeConnection(iDB2Connection conn)
        {
            if (conn == null)
            {
                return;
            }

            try
            {
                if (conn.State != ConnectionState.Closed)
                {
                    conn.Close();
                }
            }
            catch
            {
                // Evitar propagar excepciones del driver al cerrar conexión.
            }
            // No llamar Dispose() por bug conocido del driver IBM.Data.DB2.iSeries.
        }

        private static decimal SafeReadDecimal(IDataReader dr, string columnName, string origen, string codCiudad)
        {
            int idx;
            try
            {
                idx = dr.GetOrdinal(columnName);
            }
            catch (Exception ex)
            {
                LogIssue("FIELD_NOT_FOUND", origen, codCiudad, columnName, null, "No se encontró la columna decimal en el reader.", ex);
                return 0m;
            }

            try
            {
                if (dr.IsDBNull(idx))
                {
                    LogIssue("FIELD_DBNULL", origen, codCiudad, columnName, "DBNull", "Campo decimal viene DBNull; se usa 0.", null);
                    return 0m;
                }
            }
            catch (Exception ex)
            {
                LogIssue("FIELD_ISDBNULL_ERROR", origen, codCiudad, columnName, null, "Error al evaluar IsDBNull para campo decimal.", ex);
                return 0m;
            }

            object raw;
            try
            {
                raw = dr.GetValue(idx);
            }
            catch (Exception ex)
            {
                LogIssue("FIELD_READ_ERROR", origen, codCiudad, columnName, null, "Error al leer valor decimal desde DB2.", ex);
                return 0m;
            }

            if (raw == null || raw == DBNull.Value)
            {
                LogIssue("FIELD_NULL", origen, codCiudad, columnName, null, "Campo decimal viene nulo; se usa 0.", null);
                return 0m;
            }

            if (raw is decimal decimalValue) { return decimalValue; }
            if (raw is int intValue) { return intValue; }
            if (raw is long longValue) { return longValue; }
            if (raw is short shortValue) { return shortValue; }
            if (raw is double doubleValue) { return (decimal)doubleValue; }
            if (raw is float floatValue) { return (decimal)floatValue; }

            if (raw is byte[] rawBytes)
            {
                try
                {
                    var fromBytes = Encoding.UTF8.GetString(rawBytes);
                    if (!string.IsNullOrWhiteSpace(fromBytes))
                    {
                        raw = fromBytes;
                    }
                }
                catch (Exception ex)
                {
                    LogIssue("FIELD_BYTES_CONVERSION_ERROR", origen, codCiudad, columnName, "<byte[]>", "No se pudo convertir byte[] a texto decimal.", ex);
                    return 0m;
                }
            }

            var texto = Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(texto))
            {
                LogIssue("FIELD_EMPTY", origen, codCiudad, columnName, "<empty>", "Campo decimal vacío; se usa 0.", null);
                return 0m;
            }

            var limpio = texto.Trim();
            decimal resultado;
            if (decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.InvariantCulture, out resultado))
            {
                return resultado;
            }

            if (decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.CurrentCulture, out resultado))
            {
                return resultado;
            }

            LogIssue("FIELD_INVALID_DECIMAL", origen, codCiudad, columnName, limpio, "No se pudo convertir el valor decimal; se usa 0.", null);
            return 0m;
        }

        private static string SafeReadString(IDataReader dr, string columnName, string origen, string codCiudad)
        {
            int idx;
            try
            {
                idx = dr.GetOrdinal(columnName);
            }
            catch (Exception ex)
            {
                LogIssue("FIELD_NOT_FOUND", origen, codCiudad, columnName, null, "No se encontró la columna string en el reader.", ex);
                return string.Empty;
            }

            try
            {
                if (dr.IsDBNull(idx))
                {
                    LogIssue("FIELD_DBNULL", origen, codCiudad, columnName, "DBNull", "Campo string viene DBNull; se usa vacío.", null);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogIssue("FIELD_ISDBNULL_ERROR", origen, codCiudad, columnName, null, "Error al evaluar IsDBNull para campo string.", ex);
                return string.Empty;
            }

            object raw;
            try
            {
                raw = dr.GetValue(idx);
            }
            catch (Exception ex)
            {
                LogIssue("FIELD_READ_ERROR", origen, codCiudad, columnName, null, "Error al leer valor string desde DB2.", ex);
                return string.Empty;
            }

            if (raw == null || raw == DBNull.Value)
            {
                LogIssue("FIELD_NULL", origen, codCiudad, columnName, null, "Campo string viene nulo; se usa vacío.", null);
                return string.Empty;
            }

            if (raw is string rawString)
            {
                return string.IsNullOrWhiteSpace(rawString) ? string.Empty : rawString.Trim();
            }

            if (raw is byte[] rawBytes)
            {
                try
                {
                    var textoBytes = Encoding.UTF8.GetString(rawBytes);
                    return string.IsNullOrWhiteSpace(textoBytes) ? string.Empty : textoBytes.Trim();
                }
                catch (Exception ex)
                {
                    LogIssue("FIELD_BYTES_CONVERSION_ERROR", origen, codCiudad, columnName, "<byte[]>", "No se pudo convertir byte[] a string.", ex);
                    return string.Empty;
                }
            }

            try
            {
                var texto = Convert.ToString(raw, CultureInfo.InvariantCulture);
                return string.IsNullOrWhiteSpace(texto) ? string.Empty : texto.Trim();
            }
            catch (Exception ex)
            {
                LogIssue("FIELD_TO_STRING_ERROR", origen, codCiudad, columnName, "<non-string>", "No se pudo convertir el valor a string.", ex);
                return string.Empty;
            }
        }

        private static void LogIssue(
            string eventCode,
            string origen,
            string codCiudad,
            string campo,
            string valor,
            string mensaje,
            Exception ex)
        {
            var detalleEx = ex == null
                ? string.Empty
                : $" exType={ex.GetType().FullName}, exMsg={ex.Message}";

            System.Diagnostics.Debug.WriteLine(
                $"[AOCR][AS400][UbicacionUsuario][{eventCode}] origen={origen}, codCiudad={codCiudad ?? "(null)"}, campo={campo ?? "(null)"}, valor={(valor ?? "(null)")}, mensaje={mensaje}.{detalleEx}");
        }

        private static string BuildConnectionString(AS400Credentials creds)
        {
            if (string.IsNullOrWhiteSpace(creds.Server))
            {
                throw new InvalidOperationException("Servidor AS400 no configurado.");
            }

            var defaultCollection = !string.IsNullOrWhiteSpace(creds.Library)
                ? creds.Library
                : creds.Database;

            return string.Format(
                "DataSource={0};UserID={1};Password={2};DefaultCollection={3};",
                creds.Server,
                creds.UserId,
                creds.Password,
                defaultCollection);
        }
    }
}
