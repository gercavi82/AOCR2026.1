using System;
using System.Collections.Generic;
using System.Data.Odbc;
using CapaModelo;
using CapaDatos.Infrastructure;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// Clase para obtener listas de valores desde P9/AS400
    /// </summary>
    public class CD_ListaValor : AS400BaseDAO
    {
        private static CD_ListaValor _instancia;
        
        public CD_ListaValor(ISecureConfigurationService configService) : base(configService)
        {
        }

        // Constructor legacy para compatibilidad (usar solo en desarrollo)
        [Obsolete("Use el constructor con ISecureConfigurationService")]
        public CD_ListaValor() : base(new SecureConfigurationService())
        {
        }
        
        public static CD_ListaValor Instancia
        {
            get
            {
                if (_instancia == null)
                    _instancia = new CD_ListaValor();
                return _instancia;
            }
        }

        public List<tbListaValor> ListaValores(string campo)
        {
            List<tbListaValor> lstValores = new List<tbListaValor>();
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== ListaValores para campo: {campo} ===");
                
                string query = "SELECT VALVAL, VALDES FROM DGACSYS.TXDGAC WHERE VALDDS = ?";
                
                using (var connection = CreateConnection())
                {
                    System.Diagnostics.Debug.WriteLine("Intentando conectar a AS400...");
                    connection.Open();
                    System.Diagnostics.Debug.WriteLine("✅ Conexión AS400 establecida");
                    
                    using (var cmd = new OdbcCommand(query, connection))
                    {
                        cmd.Parameters.Add("@campo", OdbcType.VarChar, 50).Value = campo;
                        cmd.CommandTimeout = 30;
                        
                        System.Diagnostics.Debug.WriteLine($"Ejecutando query: {query} con campo = {campo}");
                        
                        using (var dr = cmd.ExecuteReader())
                        {
                            int count = 0;
                            while (dr.Read())
                            {
                                var oValor = new tbListaValor
                                {
                                    Codigo = dr["VALVAL"].ToString().Trim(),
                                    Descripcion = dr["VALDES"].ToString().Trim()
                                };
                                lstValores.Add(oValor);
                                count++;
                            }
                            System.Diagnostics.Debug.WriteLine($"✅ {count} registros obtenidos de AS400 para {campo}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en ListaValores para {campo}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ Tipo de error: {ex.GetType().Name}");
                
                // Fallback con bancos por defecto si falla la conexión P9
                if (campo == "OPCBAN")
                {
                    System.Diagnostics.Debug.WriteLine("🔄 Usando fallback para bancos");
                    lstValores = new List<tbListaValor>
                    {
                        new tbListaValor { Codigo = "001", Descripcion = "BANCO PICHINCHA" },
                        new tbListaValor { Codigo = "002", Descripcion = "BANCO GUAYAQUIL" },
                        new tbListaValor { Codigo = "003", Descripcion = "BANCO DEL PACÍFICO" },
                        new tbListaValor { Codigo = "004", Descripcion = "BANCO INTERNACIONAL" },
                        new tbListaValor { Codigo = "005", Descripcion = "PRODUBANCO" },
                        new tbListaValor { Codigo = "006", Descripcion = "BANCO BOLIVARIANO" },
                        new tbListaValor { Codigo = "007", Descripcion = "BANCO SOLIDARIO" },
                        new tbListaValor { Codigo = "008", Descripcion = "BANCO MACHALA" },
                        new tbListaValor { Codigo = "009", Descripcion = "BANCO LOJA" },
                        new tbListaValor { Codigo = "010", Descripcion = "COOPERATIVA 29 DE OCTUBRE" }
                    };
                }
                else if (campo == "SOLFOR")
                {
                    System.Diagnostics.Debug.WriteLine("🔄 Usando fallback para métodos de pago");
                    lstValores = new List<tbListaValor>
                    {
                        new tbListaValor { Codigo = "D", Descripcion = "DEPÓSITO BANCARIO" },
                        new tbListaValor { Codigo = "T", Descripcion = "TRANSFERENCIA BANCARIA" },
                        new tbListaValor { Codigo = "C", Descripcion = "CHEQUE" },
                        new tbListaValor { Codigo = "E", Descripcion = "EFECTIVO" },
                        new tbListaValor { Codigo = "TC", Descripcion = "TARJETA DE CRÉDITO" },
                        new tbListaValor { Codigo = "TD", Descripcion = "TARJETA DE DÉBITO" },
                        new tbListaValor { Codigo = "SPI", Descripcion = "SISTEMA DE PAGOS INTERBANCARIOS" },
                        new tbListaValor { Codigo = "PA", Descripcion = "PAGO EN AGENCIA" }
                    };
                }
            }
            
            return lstValores ?? new List<tbListaValor>();
        }
    }
}