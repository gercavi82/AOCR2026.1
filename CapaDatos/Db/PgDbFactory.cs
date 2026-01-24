using System.Configuration;
using Npgsql;
using System.Data;

namespace CapaDatos.Db
{
    public static class PgDbFactory
    {
        public static IDbConnection Create()
        {
            // En Web.config: <add name="AOCRPG" connectionString="Host=...;Port=5432;Database=...;Username=...;Password=..." providerName="Npgsql" />
            var cs = ConfigurationManager.ConnectionStrings["AOCRPG"]?.ConnectionString;
            return new NpgsqlConnection(cs);
        }
    }
}
