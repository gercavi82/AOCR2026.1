using System.Configuration;
using System.Data;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class ConexionDAO
    {
        private static readonly string _cs =
            ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
            ?? "Host=127.0.0.1;Port=5432;Database=aocr_test;Username=test;Password=test;Timeout=5;";

        // ✅ Agregado: para compatibilidad con InspeccionDAO
        public static string CadenaConexion => _cs;

        // =========================
        // NUEVO ESTILO (Dapper)
        // =========================
        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_cs);
        }

        public static NpgsqlConnection CrearConexion()
        {
            return new NpgsqlConnection(_cs);
        }

        public static string ObtenerCadenaConexion()
        {
            return _cs;
        }

        // =========================
        // ESTILO LEGADO (ADO.NET)
        // =========================
        public static NpgsqlConnection ObtenerConexion()
        {
            var con = new NpgsqlConnection(_cs);
            con.Open();
            return con;
        }

        public static void CerrarConexion(NpgsqlConnection con)
        {
            if (con == null) return;

            try
            {
                if (con.State != ConnectionState.Closed)
                    con.Close();
            }
            finally
            {
                con.Dispose();
            }
        }
    }

    public static class DataReaderExtensions
    {
        public static bool HasColumn(this System.Data.IDataRecord dr, string columnName)
        {
            for (int i = 0; i < dr.FieldCount; i++)
            {
                if (string.Equals(dr.GetName(i), columnName, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static string GetStringSafe(this System.Data.IDataRecord dr, string col)
        {
            if (!dr.HasColumn(col) || dr[col] == System.DBNull.Value)
                return string.Empty;
            return dr[col].ToString();
        }

        public static int GetIntSafe(this System.Data.IDataRecord dr, string col)
        {
            if (!dr.HasColumn(col) || dr[col] == System.DBNull.Value)
                return 0;
            try
            {
                return System.Convert.ToInt32(dr[col]);
            }
            catch
            {
                return 0;
            }
        }

        public static System.DateTime? GetDateSafe(this System.Data.IDataRecord dr, string col)
        {
            if (!dr.HasColumn(col) || dr[col] == System.DBNull.Value)
                return null;
            try
            {
                return System.Convert.ToDateTime(dr[col]);
            }
            catch
            {
                return null;
            }
        }
    }
}
