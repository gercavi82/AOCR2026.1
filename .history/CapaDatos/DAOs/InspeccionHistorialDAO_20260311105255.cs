using System;
using System.Collections.Generic;
using System.Configuration;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class InspeccionHistorialDAO
    {
        private readonly string _cs;
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;

        public InspeccionHistorialDAO()
        {
            var settings = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _cs = (settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString))
                ? settings.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public int Registrar(int codigoInspeccion, string estadoAnterior, string estadoNuevo, int? codigoUsuario, string usuarioNombre, string observacion, string origen)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    INSERT INTO public.aocr_tbhistorial_estado_inspeccion
                    (
                        codigo_inspeccion,
                        estado_anterior,
                        estado_nuevo,
                        observacion,
                        origen,
                        codigo_usuario,
                        usuario_nombre,
                        fecha_cambio
                    )
                    VALUES
                    (
                        @codigo_inspeccion,
                        @estado_anterior,
                        @estado_nuevo,
                        @observacion,
                        @origen,
                        @codigo_usuario,
                        @usuario_nombre,
                        NOW()
                    )
                    RETURNING codigo_historial;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", codigoInspeccion);
                    cmd.Parameters.AddWithValue("@estado_anterior", (object)estadoAnterior ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado_nuevo", (object)estadoNuevo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observacion", (object)observacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@origen", (object)origen ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_usuario", (object)codigoUsuario ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_nombre", (object)usuarioNombre ?? DBNull.Value);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public List<InspeccionHistorialEstado> ObtenerPorInspeccion(int codigoInspeccion)
        {
            var lista = new List<InspeccionHistorialEstado>();

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT codigo_historial,
                           codigo_inspeccion,
                           estado_anterior,
                           estado_nuevo,
                           observacion,
                           origen,
                           codigo_usuario,
                           usuario_nombre,
                           fecha_cambio
                    FROM public.aocr_tbhistorial_estado_inspeccion
                    WHERE codigo_inspeccion = @codigo_inspeccion
                    ORDER BY fecha_cambio DESC, codigo_historial DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", codigoInspeccion);
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new InspeccionHistorialEstado
                            {
                                CodigoHistorial = dr.GetInt32(dr.GetOrdinal("codigo_historial")),
                                CodigoInspeccion = dr.GetInt32(dr.GetOrdinal("codigo_inspeccion")),
                                EstadoAnterior = dr["estado_anterior"] == DBNull.Value ? null : dr["estado_anterior"].ToString(),
                                EstadoNuevo = dr["estado_nuevo"] == DBNull.Value ? null : dr["estado_nuevo"].ToString(),
                                Observacion = dr["observacion"] == DBNull.Value ? null : dr["observacion"].ToString(),
                                Origen = dr["origen"] == DBNull.Value ? null : dr["origen"].ToString(),
                                CodigoUsuario = dr["codigo_usuario"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["codigo_usuario"]),
                                UsuarioNombre = dr["usuario_nombre"] == DBNull.Value ? null : dr["usuario_nombre"].ToString(),
                                FechaCambio = dr["fecha_cambio"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(dr["fecha_cambio"])
                            });
                        }
                    }
                }
            }

            return lista;
        }

        private static void EnsureSchema(NpgsqlConnection cn)
        {
            if (_schemaReady)
            {
                return;
            }

            lock (SyncLock)
            {
                if (_schemaReady)
                {
                    return;
                }

                const string sql = @"
                    CREATE TABLE IF NOT EXISTS public.aocr_tbhistorial_estado_inspeccion
                    (
                        codigo_historial SERIAL PRIMARY KEY,
                        codigo_inspeccion INTEGER NOT NULL,
                        estado_anterior VARCHAR(120),
                        estado_nuevo VARCHAR(120) NOT NULL,
                        observacion TEXT,
                        origen VARCHAR(60),
                        codigo_usuario INTEGER,
                        usuario_nombre VARCHAR(160),
                        fecha_cambio TIMESTAMP NOT NULL DEFAULT NOW()
                    );

                    CREATE INDEX IF NOT EXISTS idx_hist_inspeccion_estado_inspeccion
                        ON public.aocr_tbhistorial_estado_inspeccion(codigo_inspeccion, fecha_cambio DESC);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }
    }
}