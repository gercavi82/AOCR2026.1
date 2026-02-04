using System;
using System.Configuration;

namespace CapaDatos.Utils
{
    /// <summary>
    /// Helper para utilidades de base de datos
    /// </summary>
    public static class DatabaseHelper
    {
        /// <summary>
        /// Obtiene la cadena de conexión desde el archivo de configuración
        /// </summary>
        public static string GetConnectionString()
        {
            return ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'AOCRConnection'");
        }
    }
}
