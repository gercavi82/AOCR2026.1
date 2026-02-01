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
            // Placeholder - implementar según necesidad
            return new List<Empresa>();
        }
    }

    /// <summary>
    /// DTO para representar una empresa del AS400
    /// </summary>
    public class Empresa
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
    }
}
