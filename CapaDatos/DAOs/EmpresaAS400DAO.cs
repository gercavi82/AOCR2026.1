using System;
using System.Data;
using IBM.Data.DB2.iSeries;
using System.Collections.Generic;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    public class EmpresaAS400DAO
    {
        private readonly ISecureConfigurationService _configService;
        private readonly string _connectionString;

        public EmpresaAS400DAO(ISecureConfigurationService configService)
        {
            _configService = configService ?? throw new ArgumentNullException("configService");

            // Construir connection string de forma segura
            var creds = _configService.GetAS400Credentials();
            _connectionString = BuildConnectionString(creds);
        }

        // Constructor legacy para compatibilidad (usar solo en desarrollo)
        [Obsolete("Use el constructor con ISecureConfigurationService")]
        public EmpresaAS400DAO()
        {
            _configService = new SecureConfigurationService();
            var creds = _configService.GetAS400Credentials();
            _connectionString = BuildConnectionString(creds);
        }

        private string BuildConnectionString(AS400Credentials creds)
        {
            // Validar que tenemos credenciales
            if (string.IsNullOrWhiteSpace(creds.Server))
            {
                throw new InvalidOperationException("Servidor AS400 no configurado. Verifique Web.config: AS400:Server, AS400:Database, AS400:UserId, AS400:Password");
            }

            // Usar biblioteca si está configurada, sino Database
            var defaultCollection = !string.IsNullOrWhiteSpace(creds.Library) ? creds.Library : creds.Database;

            // Formato para IBM.Data.DB2.iSeries
            return $"DataSource={creds.Server};UserID={creds.UserId};Password={creds.Password};DefaultCollection={defaultCollection};";
        }

        protected iDB2Connection GetConnection()
        {
            return new iDB2Connection(_connectionString);
        }

        /// <summary>
        /// Cierra la conexión iDB2 de forma segura.
        /// NO se llama Dispose() porque el driver IBM.Data.DB2.iSeries tiene un bug
        /// que lanza NullReferenceException en iDB2Connection.Dispose().
        /// Close() libera los recursos de red; el GC se encargará del objeto.
        /// </summary>
        private void SafeDisposeConnection(iDB2Connection conn)
        {
            if (conn == null) return;
            try
            {
                if (conn.State != ConnectionState.Closed)
                    conn.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ [EmpresaAS400DAO] Error al cerrar conexión: {ex.Message}");
            }
            // No llamar conn.Dispose() — bug en IBM.Data.DB2.iSeries.iDB2Connection.Dispose(Boolean)
        }

        public bool TestConnection()
        {
            iDB2Connection conn = null;
            try
            {
                conn = GetConnection();
                conn.Open();
                return conn.State == ConnectionState.Open;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error de conexión AS400: " + ex.Message);
                return false;
            }
            finally
            {
                SafeDisposeConnection(conn);
            }
        }

        public List<Empresa> ObtenerEmpresas()
        {
            var empresas = new List<Empresa>();

            iDB2Connection conn = null;
            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 [EmpresaAS400DAO] Iniciando consulta a CIAARC...");
                conn = GetConnection();
                System.Diagnostics.Debug.WriteLine($"🔗 [EmpresaAS400DAO] Connection String: {conn.ConnectionString.Replace("Password=", "Password=***")}");
                conn.Open();
                System.Diagnostics.Debug.WriteLine("✅ [EmpresaAS400DAO] Conexión abierta exitosamente");

                string query = @"
                    SELECT 
                        CIACOD as CodigoOaci, 
                        CIACO2 as CodigoIata, 
                        CIACO3 as CodigoNumeroCia, 
                        CIANOM as NombreCompaniaAviacion 
                    FROM CIAARC
                    WHERE CIAEST = 'AC'
                    ORDER BY CIANOM";

                System.Diagnostics.Debug.WriteLine($"📝 [EmpresaAS400DAO] Ejecutando query: {query}");

                using (var cmd = new iDB2Command(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        empresas.Add(new Empresa
                        {
                            CodigoOaci = SafeReadString(reader, "CodigoOaci", "ObtenerEmpresas", null),
                            CodigoIata = SafeReadString(reader, "CodigoIata", "ObtenerEmpresas", null),
                            CodigoNumeroCia = SafeReadString(reader, "CodigoNumeroCia", "ObtenerEmpresas", null),
                            Nombre = SafeReadString(reader, "NombreCompaniaAviacion", "ObtenerEmpresas", null)
                        });
                    }
                }
                System.Diagnostics.Debug.WriteLine($"✅ [EmpresaAS400DAO] {empresas.Count} empresas activas encontradas");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [EmpresaAS400DAO] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ [EmpresaAS400DAO] Tipo: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"❌ [EmpresaAS400DAO] StackTrace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("⚠️ [EmpresaAS400DAO] Se devuelve lista vacia por fallback.");
            }
            finally
            {
                SafeDisposeConnection(conn);
            }

            return empresas;
        }

        /// <summary>
        /// Obtiene una empresa específica por su código OACI
        /// </summary>
        public Empresa ObtenerEmpresaPorCodigo(string codigoOaci)
        {
            if (string.IsNullOrWhiteSpace(codigoOaci))
                return null;

            iDB2Connection conn = null;
            try
            {
                conn = GetConnection();
                conn.Open();

                string query = @"
                    SELECT 
                        CIACOD as CodigoOaci, 
                        CIACO2 as CodigoIata, 
                        CIACO3 as CodigoNumeroCia, 
                        CIANOM as NombreCompaniaAviacion 
                    FROM CIAARC
                    WHERE TRIM(CIACOD) = @codigo
                    FETCH FIRST 1 ROW ONLY";

                using (var cmd = new iDB2Command(query, conn))
                {
                    cmd.Parameters.Add("@codigo", iDB2DbType.iDB2Char).Value = codigoOaci.Trim().ToUpper();

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader != null && reader.Read())
                        {
                            return new Empresa
                            {
                                CodigoOaci = SafeReadString(reader, "CodigoOaci", "ObtenerEmpresaPorCodigo", codigoOaci),
                                CodigoIata = SafeReadString(reader, "CodigoIata", "ObtenerEmpresaPorCodigo", codigoOaci),
                                CodigoNumeroCia = SafeReadString(reader, "CodigoNumeroCia", "ObtenerEmpresaPorCodigo", codigoOaci),
                                Nombre = SafeReadString(reader, "NombreCompaniaAviacion", "ObtenerEmpresaPorCodigo", codigoOaci)
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EmpresaAS400DAO] Error obteniendo empresa codigo={codigoOaci ?? "(null)"}: tipo={ex.GetType().FullName}, msg={ex.Message}");
            }
            finally
            {
                SafeDisposeConnection(conn);
            }

            return null;
        }

        private static string SafeReadString(iDB2DataReader reader, string columnName, string metodo, string referencia)
        {
            if (reader == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EmpresaAS400DAO] {metodo}: reader nulo para columna={columnName}, referencia={referencia ?? "(null)"}.");
                return null;
            }

            int ordinal;
            try
            {
                ordinal = reader.GetOrdinal(columnName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EmpresaAS400DAO] {metodo}: columna inexistente={columnName}, referencia={referencia ?? "(null)"}, error={ex.GetType().FullName}, msg={ex.Message}");
                return null;
            }

            try
            {
                if (reader.IsDBNull(ordinal))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EmpresaAS400DAO] {metodo}: columna={columnName} viene DBNull, referencia={referencia ?? "(null)"}.");
                    return null;
                }

                var raw = reader.GetValue(ordinal);
                if (raw == null || raw == DBNull.Value)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EmpresaAS400DAO] {metodo}: columna={columnName} viene null, referencia={referencia ?? "(null)"}.");
                    return null;
                }

                var texto = Convert.ToString(raw);
                return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
            }
            catch (iDB2ConversionException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EmpresaAS400DAO] {metodo}: iDB2ConversionException columna={columnName}, referencia={referencia ?? "(null)"}, msg={ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EmpresaAS400DAO] {metodo}: error leyendo columna={columnName}, referencia={referencia ?? "(null)"}, tipo={ex.GetType().FullName}, msg={ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// DTO para representar una empresa del AS400
    /// </summary>
    public class Empresa
    {
        public string CodigoOaci { get; set; }
        public string CodigoIata { get; set; }
        public string CodigoNumeroCia { get; set; }
        public string Nombre { get; set; }

        // Propiedad legacy para compatibilidad
        public string Codigo => CodigoOaci;
    }
}
