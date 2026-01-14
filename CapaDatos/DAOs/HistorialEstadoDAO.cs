using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class HistorialEstadoDAO
    {
        private NpgsqlConnection CrearConexion()
        {
            var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
                throw new Exception("Cadena de conexión faltante.");

            return new NpgsqlConnection(cs);
        }

        private HistorialEstado Map(IDataRecord r)
        {
            return new HistorialEstado
            {
                CodigoHistorial = r["codigohistorial"] != DBNull.Value ? Convert.ToInt32(r["codigohistorial"]) : 0,
                CodigoSolicitud = r["codigosolicitud"] != DBNull.Value ? Convert.ToInt32(r["codigosolicitud"]) : 0,
                EstadoAnterior = r["estadoanterior"] != DBNull.Value ? r["estadoanterior"].ToString() : null,
                EstadoNuevo = r["estadonuevo"] != DBNull.Value ? r["estadonuevo"].ToString() : null,
                CodigoUsuario = r["codigousuario"] != DBNull.Value ? Convert.ToInt32(r["codigousuario"]) : 0,
                Observaciones = r["observaciones"] != DBNull.Value ? r["observaciones"].ToString() : null,
                FechaCambio = r["fechacambio"] != DBNull.Value ? Convert.ToDateTime(r["fechacambio"]) : DateTime.Now
            };
        }

        public List<HistorialEstado> ObtenerPorSolicitud(int codigoSolicitud)
        {
            var list = new List<HistorialEstado>();
            const string sql = @"
                SELECT codigohistorial, codigosolicitud, estadoanterior, estadonuevo,
                       codigousuario, observaciones, fechacambio
                FROM aocr_tbhistorialestado
                WHERE codigosolicitud = @id
                ORDER BY fechacambio DESC;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", codigoSolicitud);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        list.Add(Map(rd));
                }
            }
            return list;
        }

        public HistorialEstado ObtenerUltimoCambio(int codigoSolicitud)
        {
            const string sql = @"
                SELECT codigohistorial, codigosolicitud, estadoanterior, estadonuevo,
                       codigousuario, observaciones, fechacambio
                FROM aocr_tbhistorialestado
                WHERE codigosolicitud = @id
                ORDER BY fechacambio DESC
                LIMIT 1;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", codigoSolicitud);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                        return Map(rd);
                }
            }
            return null;
        }

        public List<HistorialEstado> ObtenerPorEstado(string estadoNuevo)
        {
            var list = new List<HistorialEstado>();
            const string sql = @"
                SELECT codigohistorial, codigosolicitud, estadoanterior, estadonuevo,
                       codigousuario, observaciones, fechacambio
                FROM aocr_tbhistorialestado
                WHERE estadonuevo = @estado
                ORDER BY fechacambio DESC;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@estado", (object)estadoNuevo ?? DBNull.Value);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        list.Add(Map(rd));
                }
            }
            return list;
        }

        public List<HistorialEstado> ObtenerPorUsuario(int codigoUsuario)
        {
            var list = new List<HistorialEstado>();
            const string sql = @"
                SELECT codigohistorial, codigosolicitud, estadoanterior, estadonuevo,
                       codigousuario, observaciones, fechacambio
                FROM aocr_tbhistorialestado
                WHERE codigousuario = @user
                ORDER BY fechacambio DESC;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@user", codigoUsuario);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        list.Add(Map(rd));
                }
            }
            return list;
        }

        public List<HistorialEstado> ObtenerPorFecha(DateTime desde, DateTime hasta)
        {
            var list = new List<HistorialEstado>();
            const string sql = @"
                SELECT codigohistorial, codigosolicitud, estadoanterior, estadonuevo,
                       codigousuario, observaciones, fechacambio
                FROM aocr_tbhistorialestado
                WHERE fechacambio >= @desde
                  AND fechacambio <= @hasta
                ORDER BY fechacambio DESC;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@desde", desde);
                cmd.Parameters.AddWithValue("@hasta", hasta);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        list.Add(Map(rd));
                }
            }
            return list;
        }

        public List<HistorialEstado> ObtenerPorFecha(DateTime fecha)
        {
            var desde = fecha.Date;
            var hasta = fecha.Date.AddDays(1).AddSeconds(-1);
            return ObtenerPorFecha(desde, hasta);
        }

        public bool RegistrarCambio(int codigoSolicitud, string estadoAnterior, string estadoNuevo, int codigoUsuario, string observaciones)
        {
            try
            {
                const string sql = @"
                    INSERT INTO aocr_tbhistorialestado
                    (codigosolicitud, estadoanterior, estadonuevo, codigousuario, observaciones, fechacambio)
                    VALUES
                    (@sol, @ant, @nuevo, @user, @obs, @fecha);";

                using (var cn = CrearConexion())
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@sol", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@ant", (object)estadoAnterior ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nuevo", (object)estadoNuevo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@user", codigoUsuario);
                    cmd.Parameters.AddWithValue("@obs", (object)observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now);

                    cn.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al registrar historial: " + ex.Message);
            }
        }

        public bool Insertar(HistorialEstado modelo)
        {
            if (modelo == null) return false;

            // Corrección CS1503: Validar y convertir int? a int
            if (!modelo.CodigoSolicitud.HasValue || !modelo.CodigoUsuario.HasValue)
                return false;  // No insertar si valores null

            return RegistrarCambio(
                modelo.CodigoSolicitud.Value,  // Conversión segura
                modelo.EstadoAnterior,
                modelo.EstadoNuevo,
                modelo.CodigoUsuario.Value,   // Conversión segura
                modelo.Observaciones
            );
        }
    }
}