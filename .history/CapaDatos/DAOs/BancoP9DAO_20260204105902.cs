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
        /// Obtiene la lista de bancos desde la base de datos P9
        /// </summary>
        /// <returns>Lista de bancos disponibles</returns>
        public List<BancoP9> ObtenerBancos()
        {
            var bancos = new List<BancoP9>();

            try
            {
                using (var connection = new iDB2Connection(_connectionStringP9))
                {
                    connection.Open();

                    string sql = @"
                        SELECT VALVAL, VALDES 
                        FROM DGACSYS.TXDGAC 
                        WHERE VALDDS = 'OPCBAN' 
                        ORDER BY VALDES";

                    using (var command = new iDB2Command(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var banco = new BancoP9
                            {
                                Codigo = reader.IsDBNull("VALVAL") ? string.Empty : reader.GetString("VALVAL").Trim(),
                                Descripcion = reader.IsDBNull("VALDES") ? string.Empty : reader.GetString("VALDES").Trim()
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
            catch (Exception ex)
            {
                // Log del error (puedes usar tu sistema de logging preferido)
                System.Diagnostics.Debug.WriteLine($"Error al consultar bancos P9: {ex.Message}");
                
                // En caso de error, devolver lista vacía para no interrumpir el flujo
                // Opcionalmente, puedes agregar bancos por defecto aquí
                bancos = ObtenerBancosPorDefecto();
            }

            return bancos;
        }

        /// <summary>
        /// Obtiene una lista de bancos por defecto en caso de error en la consulta
        /// </summary>
        /// <returns>Lista de bancos predeterminados</returns>
        private List<BancoP9> ObtenerBancosPorDefecto()
        {
            return new List<BancoP9>
            {
                new BancoP9 { Codigo = "001", Descripcion = "Banco del Pacífico" },
                new BancoP9 { Codigo = "002", Descripcion = "Banco Pichincha" },
                new BancoP9 { Codigo = "003", Descripcion = "Banco de Guayaquil" },
                new BancoP9 { Codigo = "004", Descripcion = "Banco Internacional" },
                new BancoP9 { Codigo = "005", Descripcion = "Produbanco" }
            };
        }
    }
}