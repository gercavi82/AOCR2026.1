using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using CapaModelo;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// DAO de Auditoría usando PostgreSQL + Dapper
    /// </summary>
    public class AuditoriaDAO
    {
        private NpgsqlConnection CrearConexion()
        {
            return ConexionDAO.CrearConexion(); // Asegúrate de tener esta clase
        }

        public virtual void Registrar(Auditoria log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            if (log.Fecha == default)
                log.Fecha = DateTime.Now;

            using (var con = CrearConexion())
            {
                con.Open();
                RegistrarEnTransaccion(con, null, log);
            }
        }

        public static void RegistrarEnTransaccion(NpgsqlConnection cn, NpgsqlTransaction tx, Auditoria log,
            int? solicitudId = null, int? usuarioId = null)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            var tipos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var meta = new NpgsqlCommand("SELECT column_name,udt_name FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_tbauditoria'", cn, tx))
            using (var rd = meta.ExecuteReader())
                while (rd.Read()) tipos.Add(rd.GetString(0), rd.GetString(1));
            var fecha = log.Fecha == default(DateTime) ? DateTime.Now : log.Fecha;
            var valores = new Dictionary<string, object>
            {
                { "entidad", log.Entidad }, { "tabla_afectada", solicitudId.HasValue ? "aocr_tbsolicitud" : log.Entidad },
                { "registro_id", solicitudId }, { "usuario_id", usuarioId },
                { "accion", log.Accion }, { "usuario", log.Usuario },
                { "fecha", fecha }, { "fecha_accion", fecha }, { "fecha_hora", fecha },
                { "datos_previos", log.DatosPrevios }, { "datos_anteriores", log.DatosPrevios },
                { "datos_nuevos", log.DatosNuevos }, { "detalle", log.DatosNuevos }, { "modulo", log.Entidad }
            };
            var campos = new List<string>();
            var parametros = new List<string>();
            using (var cmd = new NpgsqlCommand { Connection = cn, Transaction = tx })
            {
                foreach (var par in valores)
                {
                    string tipo;
                    if (!tipos.TryGetValue(par.Key, out tipo)) continue;
                    campos.Add(par.Key);
                    parametros.Add("@" + par.Key);
                    if (tipo == "jsonb" || tipo == "json")
                        cmd.Parameters.Add("@" + par.Key, tipo == "jsonb" ? NpgsqlDbType.Jsonb : NpgsqlDbType.Json).Value =
                            par.Value == null ? (object)DBNull.Value : Newtonsoft.Json.JsonConvert.SerializeObject(par.Value);
                    else
                        cmd.Parameters.AddWithValue("@" + par.Key, par.Value ?? DBNull.Value);
                }
                cmd.CommandText = "INSERT INTO public.aocr_tbauditoria (" + string.Join(",", campos) + ") VALUES (" + string.Join(",", parametros) + ")";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
