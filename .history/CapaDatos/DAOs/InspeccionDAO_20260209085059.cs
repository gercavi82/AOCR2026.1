using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class InspeccionDAO
    {
        private readonly string _cs;
        private const string TABLA = "public.aocr_tbinspeccion";

        public InspeccionDAO()
        {
            var settings = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _cs = (settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString))
                ? settings.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public Inspeccion ObtenerPorId(int id)
        {
            const string sql = @"
                SELECT 
                    codigo_inspeccion, codigo_solicitud, codigo_inspector,
                    fecha_programada, lugar, latitud, longitud, tipo,
                    observaciones_generales, comentarios, hallazgos_principales,
                    estado, resultado,
                    created_at, created_by, updated_at, updated_by
                FROM public.aocr_tbinspeccion
                WHERE codigo_inspeccion = @id;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) return null;
                    return Map(dr);
                }
            }
        }

        public List<Inspeccion> ListarTodas()
        {
            var lista = new List<Inspeccion>();

            const string sql = @"
                SELECT 
                    codigo_inspeccion, codigo_solicitud, codigo_inspector,
                    fecha_programada, lugar, latitud, longitud, tipo,
                    observaciones_generales, comentarios, hallazgos_principales,
                    estado, resultado,
                    created_at, created_by, updated_at, updated_by
                FROM public.aocr_tbinspeccion
                ORDER BY codigo_inspeccion DESC;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                        lista.Add(Map(dr));
                }
            }

            return lista;
        }

        public List<Inspeccion> ListarPorInspector(int codigoInspector)
        {
            var lista = new List<Inspeccion>();

            const string sql = @"
                SELECT 
                    codigo_inspeccion, codigo_solicitud, codigo_inspector,
                    fecha_programada, lugar, latitud, longitud, tipo,
                    observaciones_generales, comentarios, hallazgos_principales,
                    estado, resultado,
                    created_at, created_by, updated_at, updated_by
                FROM public.aocr_tbinspeccion
                WHERE codigo_inspector = @ci
                ORDER BY fecha_programada DESC NULLS LAST, codigo_inspeccion DESC;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@ci", codigoInspector);
                cn.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                        lista.Add(Map(dr));
                }
            }

            return lista;
        }

        public List<Inspeccion> ListarPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<Inspeccion>();

            const string sql = @"
                SELECT 
                    codigo_inspeccion, codigo_solicitud, codigo_inspector,
                    fecha_programada, lugar, latitud, longitud, tipo,
                    observaciones_generales, comentarios, hallazgos_principales,
                    estado, resultado,
                    created_at, created_by, updated_at, updated_by
                FROM public.aocr_tbinspeccion
                WHERE codigo_solicitud = @cs
                ORDER BY fecha_programada DESC NULLS LAST, codigo_inspeccion DESC;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@cs", codigoSolicitud);
                cn.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                        lista.Add(Map(dr));
                }
            }

            return lista;
        }

        public int Crear(Inspeccion i)
        {
            const string sql = @"
                INSERT INTO public.aocr_tbinspeccion
                (codigo_solicitud, codigo_inspector, fecha_programada,
                 lugar, latitud, longitud, tipo, observaciones_generales, comentarios, hallazgos_principales,
                 estado, resultado, created_at, created_by, updated_at, updated_by)
                VALUES
                (@sol, @insp, @fp,
                 @lug, @lat, @lon, @tipo, @obs, @com, @hall,
                 @estado, @res, NOW(), @cby, NOW(), @uby)
                RETURNING codigo_inspeccion;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@sol", i.CodigoSolicitud);
                cmd.Parameters.AddWithValue("@insp", (object)i.CodigoInspector ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fp", (object)i.FechaProgramada ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@lug", (object)i.Lugar ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lat", (object)i.Latitud ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lon", (object)i.Longitud ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tipo", (object)i.Tipo ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@obs", (object)i.ObservacionesGenerales ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@com", (object)i.Comentarios ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@hall", (object)i.HallazgosPrincipales ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@estado", (object)i.Estado ?? "CREADA");
                cmd.Parameters.AddWithValue("@res", (object)i.Resultado ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruta", (object)i.RutaInforme ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@cby", (object)i.CreatedBy ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uby", (object)i.UpdatedBy ?? DBNull.Value);

                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public bool Actualizar(Inspeccion i)
        {
            const string sql = @"
                UPDATE public.aocr_tbinspeccion
                SET
                    codigo_inspector = @insp,
                    fecha_programada = @fp,
                    lugar = @lug,
                    latitud = @lat,
                    longitud = @lon,
                    tipo = @tipo,
                    observaciones_generales = @obs,
                    comentarios = @com,
                    hallazgos_principales = @hall,
                    estado = @estado,
                    resultado = @res,
                    updated_at = NOW(),
                    updated_by = @uby
                WHERE codigo_inspeccion = @id;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", i.CodigoInspeccion);

                cmd.Parameters.AddWithValue("@insp", (object)i.CodigoInspector ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fp", (object)i.FechaProgramada ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@lug", (object)i.Lugar ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lat", (object)i.Latitud ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lon", (object)i.Longitud ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tipo", (object)i.Tipo ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@obs", (object)i.ObservacionesGenerales ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@com", (object)i.Comentarios ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@hall", (object)i.HallazgosPrincipales ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@estado", (object)i.Estado ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@res", (object)i.Resultado ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruta", (object)i.RutaInforme ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@uby", (object)i.UpdatedBy ?? DBNull.Value);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool CambiarEstado(int id, string estado, int updatedBy)
        {
            const string sql = @"
                UPDATE public.aocr_tbinspeccion
                SET estado = @estado, updated_at = NOW(), updated_by = @uby
                WHERE codigo_inspeccion = @id;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@estado", estado);
                cmd.Parameters.AddWithValue("@uby", updatedBy);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool GuardarInforme(int id, string rutaInforme, int updatedBy)
        {
            const string sql = @"
                UPDATE public.aocr_tbinspeccion
                SET ruta_informe = @ruta, updated_at = NOW(), updated_by = @uby
                WHERE codigo_inspeccion = @id;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@ruta", (object)rutaInforme ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uby", updatedBy);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool Cerrar(int id, string resultado, int updatedBy)
        {
            // si tu tabla NO tiene fecha_cierre, quítala del SQL.
            const string sql = @"
                UPDATE public.aocr_tbinspeccion
                SET estado = 'CERRADA',
                    resultado = @res,
                    updated_at = NOW(),
                    updated_by = @uby
                WHERE codigo_inspeccion = @id;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@res", (object)resultado ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uby", updatedBy);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private Inspeccion Map(IDataRecord dr)
        {
            // Mapeo por nombre (más robusto)
            return new Inspeccion
            {
                CodigoInspeccion = dr["codigo_inspeccion"] != DBNull.Value ? Convert.ToInt32(dr["codigo_inspeccion"]) : 0,
                CodigoSolicitud = dr["codigo_solicitud"] != DBNull.Value ? Convert.ToInt32(dr["codigo_solicitud"]) : 0,
                CodigoInspector = dr["codigo_inspector"] != DBNull.Value ? (int?)Convert.ToInt32(dr["codigo_inspector"]) : null,

                FechaProgramada = dr["fecha_programada"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(dr["fecha_programada"]) : null,

                Lugar = dr["lugar"] != DBNull.Value ? dr["lugar"].ToString() : null,
                Latitud = dr["latitud"] != DBNull.Value ? dr["latitud"].ToString() : null,
                Longitud = dr["longitud"] != DBNull.Value ? dr["longitud"].ToString() : null,

                Tipo = dr["tipo"] != DBNull.Value ? dr["tipo"].ToString() : null,
                ObservacionesGenerales = dr["observaciones_generales"] != DBNull.Value ? dr["observaciones_generales"].ToString() : null,
                Comentarios = dr["comentarios"] != DBNull.Value ? dr["comentarios"].ToString() : null,
                HallazgosPrincipales = dr["hallazgos_principales"] != DBNull.Value ? dr["hallazgos_principales"].ToString() : null,

                Estado = dr["estado"] != DBNull.Value ? dr["estado"].ToString() : null,
                Resultado = dr["resultado"] != DBNull.Value ? dr["resultado"].ToString() : null,

                CreatedAt = dr["created_at"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(dr["created_at"]) : null,
                CreatedBy = dr["created_by"] != DBNull.Value ? (int?)Convert.ToInt32(dr["created_by"]) : null,
                UpdatedAt = dr["updated_at"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(dr["updated_at"]) : null,
                UpdatedBy = dr["updated_by"] != DBNull.Value ? (int?)Convert.ToInt32(dr["updated_by"]) : null
            };
        }

    }
}
