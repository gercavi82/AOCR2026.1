using System;
using System.Configuration;
using Dapper;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class OrdenEstadoHistorialDAO
    {
        private readonly string _connectionString;

        public OrdenEstadoHistorialDAO()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["PostgreSQL"]?.ConnectionString
                ?? ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                ?? ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString
                ?? string.Empty;
        }

        public bool RegistrarCambio(int ordenId, string estadoAnterior, string estadoNuevo, string observaciones, string usuario, string rol)
        {
            if (ordenId <= 0) return false;

            const string sql = @"
                INSERT INTO aocr_or_estado_historial
                    (orden_id, estado_anterior, estado_nuevo, observaciones, usuario, rol, fecha)
                VALUES
                    (@orden_id, @estado_anterior, @estado_nuevo, @observaciones, @usuario, @rol, NOW())";

            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                var rows = cn.Execute(sql, new
                {
                    orden_id = ordenId,
                    estado_anterior = estadoAnterior,
                    estado_nuevo = estadoNuevo,
                    observaciones = observaciones,
                    usuario = usuario,
                    rol = rol
                });
                return rows > 0;
            }
        }
    }
}

