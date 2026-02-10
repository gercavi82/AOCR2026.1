using System;
using System.Data;
using System.Data.Odbc;
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
                throw new InvalidOperationException("Servidor AS400 no configurado.");
            }

            return string.Format(
                "Driver={{IBM i Access ODBC Driver}};System={0};Database={1};Uid={2};Pwd={3};",
                creds.Server,
                creds.Database,
                creds.UserId,
                creds.Password);
        }

        protected OdbcConnection GetConnection()
        {
            return new OdbcConnection(_connectionString);
        }

        public bool TestConnection()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    return conn.State == ConnectionState.Open;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error de conexión AS400: " + ex.Message);
                return false;
            }
        }

        public List<Empresa> ObtenerEmpresas()
        {
            var empresas = new List<Empresa>();

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            CIACOD as CodigoOaci, 
                            CIACO2 as CodigoIata, 
                            CIACO3 as CodigoNumeroCia, 
                            CIANOM as NombreCompaniaAviacion 
                        FROM CIAARC
                        WHERE CIAEST = 'AC'
                        ORDER BY CIANOM";

                    using (var cmd = new OdbcCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            empresas.Add(new Empresa
                            {
                                CodigoOaci = reader["CodigoOaci"]?.ToString()?.Trim(),
                                CodigoIata = reader["CodigoIata"]?.ToString()?.Trim(),
                                CodigoNumeroCia = reader["CodigoNumeroCia"]?.ToString()?.Trim(),
                                Nombre = reader["NombreCompaniaAviacion"]?.ToString()?.Trim()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo empresas de CIAARC: {ex.Message}");
                throw new Exception("Error al consultar compañías aéreas del AS/400: " + ex.Message, ex);
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

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            CIACOD as CodigoOaci, 
                            CIACO2 as CodigoIata, 
                            CIACO3 as CodigoNumeroCia, 
                            CIANOM as NombreCompaniaAviacion 
                        FROM CIAARC
                        WHERE TRIM(CIACOD) = ?
                        FETCH FIRST 1 ROW ONLY";

                    using (var cmd = new OdbcCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigo", codigoOaci.Trim().ToUpper());

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Empresa
                                {
                                    CodigoOaci = reader["CodigoOaci"]?.ToString()?.Trim(),
                                    CodigoIata = reader["CodigoIata"]?.ToString()?.Trim(),
                                    CodigoNumeroCia = reader["CodigoNumeroCia"]?.ToString()?.Trim(),
                                    Nombre = reader["NombreCompaniaAviacion"]?.ToString()?.Trim()
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo empresa {codigoOaci} de CIAARC: {ex.Message}");
            }

            return null;
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
