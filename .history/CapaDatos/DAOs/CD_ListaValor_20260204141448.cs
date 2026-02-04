using System;
using System.Collections.Generic;
using IBM.Data.DB2.iSeries;
using CapaModelo;
using CapaDatos.Infrastructure;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// Clase para obtener listas de valores desde P9/AS400
    /// </summary>
    public class CD_ListaValor : AS400BaseDAO
    {
        private static CD_ListaValor _instancia;
        
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
                string query = "SELECT VALVAL, VALDES FROM DGACSYS.TXDGAC WHERE VALDDS = @campo";
                
                using (var connection = GetConnection())
                {
                    connection.Open();
                    using (var cmd = new iDB2Command(query, connection))
                    {
                        cmd.Parameters.Add("@campo", iDB2DbType.iDB2VarChar, 50).Value = campo;
                        
                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                var oValor = new tbListaValor
                                {
                                    Codigo = dr["VALVAL"].ToString().Trim(),
                                    Descripcion = dr["VALDES"].ToString().Trim()
                                };
                                lstValores.Add(oValor);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ListaValores: {ex.Message}");
                
                // Fallback con bancos por defecto si falla la conexión P9
                if (campo == "OPCBAN")
                {
                    lstValores = new List<tbListaValor>
                    {
                        new tbListaValor { Codigo = "001", Descripcion = "BANCO PICHINCHA" },
                        new tbListaValor { Codigo = "002", Descripcion = "BANCO GUAYAQUIL" },
                        new tbListaValor { Codigo = "003", Descripcion = "BANCO PACIFICO" },
                        new tbListaValor { Codigo = "004", Descripcion = "BANCO INTERNACIONAL" },
                        new tbListaValor { Codigo = "005", Descripcion = "PRODUBANCO" }
                    };
                }
            }
            
            return lstValores ?? new List<tbListaValor>();
        }
    }
}