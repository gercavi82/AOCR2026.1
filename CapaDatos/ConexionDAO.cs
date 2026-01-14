using System;
using System.Configuration;
using System.Data;
using Npgsql;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// Clase centralizada para manejar conexiones a la base de datos PostgreSQL.
    /// Compatible con ADO.NET y Dapper.
    /// </summary>
    public static class ConexionDAO
    {
        // =============================
        // Cadena de conexión (thread-safe, read-only)
        // =============================
        private static readonly string _cs =
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        /// <summary>
        /// Obtiene la cadena de conexión actual (de solo lectura).
        /// </summary>
        public static string CadenaConexion => _cs;

        // =============================
        // CONEXIONES PARA DAPPER
        // =============================

        /// <summary>
        /// Obtiene una nueva conexión sin abrir (útil para Dapper).
        /// </summary>
        public static NpgsqlConnection CrearConexion()
        {
            return new NpgsqlConnection(_cs);
        }

        /// <summary>
        /// Crea una conexión lista para abrir usando Dapper o ADO.NET.
        /// </summary>
        public static NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_cs);
        }

        /// <summary>
        /// Devuelve la cadena de conexión (útil para debugging o factories).
        /// </summary>
        public static string ObtenerCadenaConexion()
        {
            return _cs;
        }

        // =============================
        // CONEXIONES ESTILO ADO.NET
        // =============================

        /// <summary>
        /// Obtiene una conexión abierta (estilo ADO.NET clásico).
        /// </summary>
        public static NpgsqlConnection ObtenerConexion()
        {
            var conexion = new NpgsqlConnection(_cs);

            try
            {
                conexion.Open();
                return conexion;
            }
            catch (Exception ex)
            {
                // Puedes registrar el error si tienes logging
                throw new Exception("Error al abrir la conexión con la base de datos.", ex);
            }
        }

        /// <summary>
        /// Cierra y libera los recursos de una conexión Npgsql.
        /// </summary>
        public static void CerrarConexion(NpgsqlConnection conexion)
        {
            if (conexion == null)
                return;

            try
            {
                if (conexion.State != ConnectionState.Closed)
                    conexion.Close();
            }
            finally
            {
                conexion.Dispose();
            }
        }
    }
}
