using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using CapaModelo;
using CapaDatos.Infrastructure;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// DAO para consultar bancos desde la base de datos P9/AS400
    /// </summary>
    public class BancoP9DAO : AS400BaseDAO
    {
        public BancoP9DAO(ISecureConfigurationService configService) : base(configService)
        {
        }

        // Constructor legacy para compatibilidad (usar solo en desarrollo)
        [Obsolete("Use el constructor con ISecureConfigurationService")]
        public BancoP9DAO() : base(new SecureConfigurationService())
        {
        }

        /// <summary>
        /// Obtiene la lista de bancos desde la base de datos P9/AS400
        /// </summary>
        /// <returns>Lista de bancos disponibles</returns>
        public List<BancoP9> ObtenerBancos()
        {
            var bancos = new List<BancoP9>();

            try
            {
                // TODO: ODBC driver not available - temporary fallback with common banks
                // Remove this section and uncomment P9 code once ODBC is properly configured
                bancos.Add(new BancoP9 { Codigo = "001", Descripcion = "BANCO CENTRAL DEL ECUADOR" });
                bancos.Add(new BancoP9 { Codigo = "002", Descripcion = "BANCO PICHINCHA" });
                bancos.Add(new BancoP9 { Codigo = "003", Descripcion = "BANCO DEL PACIFICO" });
                bancos.Add(new BancoP9 { Codigo = "004", Descripcion = "BANCO GUAYAQUIL" });
                bancos.Add(new BancoP9 { Codigo = "005", Descripcion = "BANCO INTERNACIONAL" });
                bancos.Add(new BancoP9 { Codigo = "006", Descripcion = "BANCO BOLIVARIANO" });
                bancos.Add(new BancoP9 { Codigo = "007", Descripcion = "BANCO MACHALA" });
                bancos.Add(new BancoP9 { Codigo = "008", Descripcion = "BANCO PRODUBANCO" });
                
                return bancos;

                /*
                // Original P9 implementation - requires ODBC configuration
                using (var connection = GetConnection())
                {
                    connection.Open();

                    string sql = @"
                        SELECT VALVAL, VALDES 
                        FROM DGACSYS.TXDGAC 
                        WHERE VALDDS = 'OPCBAN' 
                        ORDER BY VALDES";

                    using (var command = new OdbcCommand(sql, connection))
                    {
                        command.CommandTimeout = _commandTimeout;
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var banco = new BancoP9
                                {
                                    Codigo = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim(),
                                    Descripcion = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim()
                                };

                                // Solo agregar si tiene código y descripción válidos
                                if (!string.IsNullOrEmpty(banco.Codigo) && !string.IsNullOrEmpty(banco.Descripcion))
                                {
                                    bancos.Add(banco);
                                }
                            }
                        }
                    }
                }
                */
            }
            catch (Exception ex)
            {
                // Log error but don't break the application
                System.Diagnostics.Debug.WriteLine($"Error al consultar bancos P9: {ex.Message}");
                
                // Return default banks as fallback
                if (bancos.Count == 0)
                {
                    bancos.Add(new BancoP9 { Codigo = "001", Descripcion = "BANCO CENTRAL DEL ECUADOR" });
                    bancos.Add(new BancoP9 { Codigo = "002", Descripcion = "BANCO PICHINCHA" });
                    bancos.Add(new BancoP9 { Codigo = "003", Descripcion = "BANCO DEL PACIFICO" });
                }
            }

            return bancos;
        }

        /// <summary>
        /// Obtiene conexión ODBC para AS400
        /// </summary>
        /// <returns>Conexión ODBC configurada</returns>
        protected OdbcConnection GetConnection()
        {
            return new OdbcConnection(_connectionString);
        }
    }
}