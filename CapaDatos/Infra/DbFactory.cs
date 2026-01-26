using System;
using System.Configuration;
using System.Data;
using Npgsql;

namespace CapaDatos.Infra
{
    public static class DbFactory
    {
        private const string ConnName = "AOCRConnection";

        public static IDbConnection CreateConnection()
        {
            var cs = ConfigurationManager.ConnectionStrings[ConnName]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException($"Falta ConnectionStrings['{ConnName}'] en web.config");

            return new NpgsqlConnection(cs);
        }
    }
}
