using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Linq;
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
                
                bancos.Add(new BancoP9 { Codigo = "001", Descripcion = "BANCO PICHINCHA" });                           
                bancos.Add(new BancoP9 { Codigo = "002", Descripcion = "BANCO INTERNACIONAL" });
                bancos.Add(new BancoP9 { Codigo = "003si" +
                    "", Descripcion = "BANCO RUMIÑAHUI" });
                
                
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

        /// <summary>
        /// Método de diagnóstico para probar la conexión AS400
        /// </summary>
        /// <returns>Estado de la conexión</returns>
        public string ProbarConexionAS400()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== PROBANDO CONEXIÓN AS400 ===");
                
                // Verificar driver ODBC primero
                var driverCheck = VerificarDriverODBC();
                if (!driverCheck.StartsWith("✅"))
                {
                    return driverCheck;
                }
                
                // Mostrar string de conexión (sin password)
                var connStringSafe = _connectionString.Replace("Pwd=" + new SecureConfigurationService().GetAS400Credentials().Password, "Pwd=****");
                System.Diagnostics.Debug.WriteLine($"Connection String: {connStringSafe}");
                
                using (var connection = GetConnection())
                {
                    System.Diagnostics.Debug.WriteLine("Intentando conectar...");
                    connection.Open();
                    
                    System.Diagnostics.Debug.WriteLine("✅ Conexión establecida correctamente");
                    
                    // Probar una consulta simple
                    var testSql = "SELECT COUNT(*) FROM DGACSYS.TXDGAC WHERE VALDDS = 'OPCBAN'";
                    using (var cmd = new OdbcCommand(testSql, connection))
                    {
                        cmd.CommandTimeout = 30;
                        var result = cmd.ExecuteScalar();
                        System.Diagnostics.Debug.WriteLine($"✅ Consulta de prueba exitosa: {result} bancos encontrados");
                        return $"OK - Conexión exitosa. Bancos disponibles: {result}";
                    }
                }
            }
            catch (OdbcException ex)
            {
                var error = $"❌ Error ODBC: [{ex.ErrorCode}] {ex.Message}";
                System.Diagnostics.Debug.WriteLine(error);
                
                // Detalles específicos de error AS400
                if (ex.Message.Contains("SQL30082N"))
                {
                    error += " - Posible problema de conectividad de red";
                }
                else if (ex.Message.Contains("SQL30061N"))
                {
                    error += " - Timeout de conexión";
                }
                else if (ex.Message.Contains("SQL30020N"))
                {
                    error += " - Credenciales inválidas";
                }
                
                return error;
            }
            catch (Exception ex)
            {
                var error = $"❌ Error General: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(error);
                return error;
            }
        }

        /// <summary>
        /// Verifica si los drivers ODBC necesarios están instalados
        /// </summary>
        /// <returns>Estado de los drivers</returns>
        public string VerificarDriverODBC()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== VERIFICANDO DRIVERS ODBC ===");
                
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers"))
                {
                    if (key == null)
                    {
                        return "❌ No se puede acceder al registro de drivers ODBC";
                    }
                    
                    var driverNames = key.GetValueNames();
                    var driversEncontrados = new System.Collections.Generic.List<string>();
                    
                    foreach (var driverName in driverNames)
                    {
                        var driverValue = key.GetValue(driverName)?.ToString();
                        if (driverValue == "Installed")
                        {
                            driversEncontrados.Add(driverName);
                            System.Diagnostics.Debug.WriteLine($"Driver encontrado: {driverName}");
                        }
                    }
                    
                    // Buscar drivers IBM específicos
                    var ibmDrivers = driversEncontrados.FindAll(d => 
                        d.Contains("IBM") && 
                        (d.Contains("Access") || d.Contains("i Access") || d.Contains("AS/400") || d.Contains("DB2")));
                    
                    if (ibmDrivers.Count > 0)
                    {
                        var resultado = $"✅ Drivers IBM encontrados: {string.Join(", ", ibmDrivers)}";
                        System.Diagnostics.Debug.WriteLine(resultado);
                        return resultado;
                    }
                    else
                    {
                        var mensaje = "❌ No se encontró 'IBM i Access ODBC Driver'. ";
                        mensaje += $"Drivers disponibles: {string.Join(", ", driversEncontrados.Take(10))}";
                        
                        if (driversEncontrados.Count > 10)
                        {
                            mensaje += $" (y {driversEncontrados.Count - 10} más)";
                        }
                        
                        System.Diagnostics.Debug.WriteLine(mensaje);
                        return mensaje;
                    }
                }
            }
            catch (Exception ex)
            {
                var error = $"❌ Error verificando drivers: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(error);
                return error;
            }
        }

        /// <summary>
        /// Lista todos los drivers ODBC instalados (para diagnóstico)
        /// </summary>
        /// <returns>Lista de drivers</returns>
        public string ListarDriversODBC()
        {
            try
            {
                var resultado = new System.Text.StringBuilder();
                resultado.AppendLine("=== DRIVERS ODBC INSTALADOS ===");
                
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers"))
                {
                    if (key == null)
                    {
                        return "❌ No se puede acceder al registro de drivers ODBC";
                    }
                    
                    var driverNames = key.GetValueNames();
                    var count = 0;
                    
                    foreach (var driverName in driverNames.OrderBy(x => x))
                    {
                        var driverValue = key.GetValue(driverName)?.ToString();
                        if (driverValue == "Installed")
                        {
                            count++;
                            resultado.AppendLine($"{count}. {driverName}");
                            
                            // Marcar drivers IBM
                            if (driverName.Contains("IBM"))
                            {
                                resultado.AppendLine($"   ⭐ DRIVER IBM DETECTADO");
                            }
                        }
                    }
                    
                    if (count == 0)
                    {
                        resultado.AppendLine("❌ No se encontraron drivers ODBC instalados");
                    }
                    else
                    {
                        resultado.AppendLine($"\nTotal: {count} drivers encontrados");
                    }
                }
                
                return resultado.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Error listando drivers: {ex.Message}";
            }
        }
    }
}