using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class HallazgoDAO
    {
        private string CS => ConexionDAO.CadenaConexion;
        private const string TABLA = "public.aocr_tbhallazgo";
        private const string SQLSTATE_RELATION_DOES_NOT_EXIST = "42P01";
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;

        private static bool EsRelacionNoExiste(PostgresException ex)
        {
            return ex != null && string.Equals(ex.SqlState, SQLSTATE_RELATION_DOES_NOT_EXIST, StringComparison.Ordinal);
        }

        // ✅ Método auxiliar para mapeo
        private Hallazgo MapearDesdeDataReader(NpgsqlDataReader dr)
        {
            try
            {
                return new Hallazgo
                {
                    CodigoHallazgo = dr.GetInt32(dr.GetOrdinal("codigo_hallazgo")),
                    CodigoInspeccion = dr.GetInt32(dr.GetOrdinal("codigo_inspeccion")),
                    Descripcion = dr.IsDBNull(dr.GetOrdinal("descripcion")) ? null : dr.GetString(dr.GetOrdinal("descripcion")),
                    Criticidad = dr.IsDBNull(dr.GetOrdinal("criticidad")) ? null : dr.GetString(dr.GetOrdinal("criticidad")),
                    Estado = dr.IsDBNull(dr.GetOrdinal("estado")) ? null : dr.GetString(dr.GetOrdinal("estado")),
                    AccionCorrectiva = dr.IsDBNull(dr.GetOrdinal("accion_correctiva")) ? null : dr.GetString(dr.GetOrdinal("accion_correctiva")),
                    FechaDeteccion = dr.IsDBNull(dr.GetOrdinal("fecha_deteccion")) ? (DateTime?)null : dr.GetDateTime(dr.GetOrdinal("fecha_deteccion")),
                    FechaCierre = dr.IsDBNull(dr.GetOrdinal("fecha_cierre")) ? (DateTime?)null : dr.GetDateTime(dr.GetOrdinal("fecha_cierre")),
                    Responsable = dr.IsDBNull(dr.GetOrdinal("responsable")) ? null : dr.GetString(dr.GetOrdinal("responsable")),
                    CreatedAt = dr.IsDBNull(dr.GetOrdinal("created_at")) ? (DateTime?)null : dr.GetDateTime(dr.GetOrdinal("created_at")),
                    CreatedBy = dr.IsDBNull(dr.GetOrdinal("created_by")) ? null : dr.GetString(dr.GetOrdinal("created_by")),
                    UpdatedAt = dr.IsDBNull(dr.GetOrdinal("updated_at")) ? (DateTime?)null : dr.GetDateTime(dr.GetOrdinal("updated_at")),
                    UpdatedBy = dr.IsDBNull(dr.GetOrdinal("updated_by")) ? null : dr.GetString(dr.GetOrdinal("updated_by"))
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al mapear hallazgo: {ex.Message}");
                return null;
            }
        }

        // ✅ Insertar nuevo hallazgo
        public int Insertar(Hallazgo hallazgo)
        {
            try
            {
                using (var conn = new NpgsqlConnection(CS))
                {
                    conn.Open();
                    EnsureSchema(conn);

                    string sql = $@"
                        INSERT INTO {TABLA}
                        (codigo_inspeccion, descripcion, criticidad, estado, fecha_deteccion, created_at, created_by, updated_at, updated_by)
                        VALUES
                        (@codigoInspeccion, @descripcion, @criticidad, @estado, @fechaDeteccion, NOW(), @createdBy, NOW(), @updatedBy)
                        RETURNING codigo_hallazgo;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoInspeccion", hallazgo.CodigoInspeccion);
                        cmd.Parameters.AddWithValue("@descripcion", (object)hallazgo.Descripcion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@criticidad", (object)hallazgo.Criticidad ?? "MEDIA");
                        cmd.Parameters.AddWithValue("@estado", (object)hallazgo.Estado ?? "ABIERTO");
                        cmd.Parameters.AddWithValue("@fechaDeteccion", (object)hallazgo.FechaDeteccion ?? DateTime.Now);
                        cmd.Parameters.AddWithValue("@createdBy", (object)hallazgo.CreatedBy ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@updatedBy", (object)hallazgo.UpdatedBy ?? DBNull.Value);

                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (PostgresException ex) when (EsRelacionNoExiste(ex))
            {
                // La tabla de hallazgos puede no estar desplegada en ciertos ambientes.
                // Degradar sin romper el flujo principal de inspección.
                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en Insertar: {ex.Message}");
                return 0;
            }
        }

        // ✅ Actualizar hallazgo
        public bool Actualizar(Hallazgo hallazgo)
        {
            try
            {
                using (var conn = new NpgsqlConnection(CS))
                {
                    conn.Open();
                    EnsureSchema(conn);

                    string sql = $@"
                        UPDATE {TABLA}
                        SET
                            descripcion = @descripcion,
                            criticidad = @criticidad,
                            estado = @estado,
                            accion_correctiva = @accionCorrectiva,
                            fecha_cierre = @fechaCierre,
                            responsable = @responsable,
                            updated_at = NOW(),
                            updated_by = @updatedBy
                        WHERE codigo_hallazgo = @codigoHallazgo;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@descripcion", (object)hallazgo.Descripcion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@criticidad", (object)hallazgo.Criticidad ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@estado", (object)hallazgo.Estado ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@accionCorrectiva", (object)hallazgo.AccionCorrectiva ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fechaCierre", (object)hallazgo.FechaCierre ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@responsable", (object)hallazgo.Responsable ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@updatedBy", (object)hallazgo.UpdatedBy ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@codigoHallazgo", hallazgo.CodigoHallazgo);

                        int filasAfectadas = cmd.ExecuteNonQuery();
                        return filasAfectadas > 0;
                    }
                }
            }
            catch (PostgresException ex) when (EsRelacionNoExiste(ex))
            {
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en Actualizar: {ex.Message}");
                return false;
            }
        }

        // ✅ Obtener hallazgos por inspección
        public List<Hallazgo> ObtenerPorInspeccion(int codigoInspeccion)
        {
            var lista = new List<Hallazgo>();

            try
            {
                using (var conn = new NpgsqlConnection(CS))
                {
                    conn.Open();
                    EnsureSchema(conn);

                    string sql = $@"
                        SELECT *
                        FROM {TABLA}
                        WHERE codigo_inspeccion = @codigoInspeccion
                        ORDER BY criticidad DESC, fecha_deteccion DESC;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoInspeccion", codigoInspeccion);

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                var hallazgo = MapearDesdeDataReader(dr);
                                if (hallazgo != null)
                                    lista.Add(hallazgo);
                            }
                        }
                    }
                }
            }
            catch (PostgresException ex) when (EsRelacionNoExiste(ex))
            {
                // Sin tabla de hallazgos, responder vacío para no interrumpir Detalle.
                return lista;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerPorInspeccion: {ex.Message}");
            }

            return lista;
        }

        public Hallazgo ObtenerPorId(int codigoHallazgo)
        {
            if (codigoHallazgo <= 0)
            {
                return null;
            }

            try
            {
                using (var conn = new NpgsqlConnection(CS))
                {
                    conn.Open();
                    EnsureSchema(conn);

                    string sql = $@"
                        SELECT *
                        FROM {TABLA}
                        WHERE codigo_hallazgo = @codigoHallazgo
                        LIMIT 1;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoHallazgo", codigoHallazgo);

                        using (var dr = cmd.ExecuteReader())
                        {
                            return dr.Read() ? MapearDesdeDataReader(dr) : null;
                        }
                    }
                }
            }
            catch (PostgresException ex) when (EsRelacionNoExiste(ex))
            {
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerPorId: {ex.Message}");
                return null;
            }
        }

        // ✅ Cerrar hallazgo
        public bool CerrarHallazgo(int codigoHallazgo, string accionCorrectiva, string responsable, string usuario)
        {
            try
            {
                using (var conn = new NpgsqlConnection(CS))
                {
                    conn.Open();
                    EnsureSchema(conn);

                    string sql = $@"
                        UPDATE {TABLA}
                        SET
                            estado = 'CERRADO',
                            accion_correctiva = @accionCorrectiva,
                            responsable = @responsable,
                            fecha_cierre = NOW(),
                            updated_at = NOW(),
                            updated_by = @updatedBy
                        WHERE codigo_hallazgo = @codigoHallazgo;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@accionCorrectiva", (object)accionCorrectiva ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@responsable", (object)responsable ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@updatedBy", (object)usuario ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@codigoHallazgo", codigoHallazgo);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (PostgresException ex) when (EsRelacionNoExiste(ex))
            {
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en CerrarHallazgo: {ex.Message}");
                return false;
            }
        }

        // ✅ Obtener estadísticas de hallazgos por inspección
        public Dictionary<string, int> ObtenerEstadisticas(int codigoInspeccion)
        {
            var estadisticas = new Dictionary<string, int>
            {
                { "TOTAL", 0 },
                { "ALTA", 0 },
                { "MEDIA", 0 },
                { "BAJA", 0 },
                { "ABIERTO", 0 },
                { "CERRADO", 0 }
            };

            try
            {
                using (var conn = new NpgsqlConnection(CS))
                {
                    conn.Open();
                    EnsureSchema(conn);

                    string sql = $@"
                        SELECT 
                            COUNT(*) as total,
                            COUNT(CASE WHEN criticidad = 'ALTA' THEN 1 END) as alta,
                            COUNT(CASE WHEN criticidad = 'MEDIA' THEN 1 END) as media,
                            COUNT(CASE WHEN criticidad = 'BAJA' THEN 1 END) as baja,
                            COUNT(CASE WHEN estado = 'ABIERTO' THEN 1 END) as abierto,
                            COUNT(CASE WHEN estado = 'CERRADO' THEN 1 END) as cerrado
                        FROM {TABLA}
                        WHERE codigo_inspeccion = @codigoInspeccion;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoInspeccion", codigoInspeccion);

                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                estadisticas["TOTAL"] = dr.IsDBNull(dr.GetOrdinal("total")) ? 0 : dr.GetInt32(dr.GetOrdinal("total"));
                                estadisticas["ALTA"] = dr.IsDBNull(dr.GetOrdinal("alta")) ? 0 : dr.GetInt32(dr.GetOrdinal("alta"));
                                estadisticas["MEDIA"] = dr.IsDBNull(dr.GetOrdinal("media")) ? 0 : dr.GetInt32(dr.GetOrdinal("media"));
                                estadisticas["BAJA"] = dr.IsDBNull(dr.GetOrdinal("baja")) ? 0 : dr.GetInt32(dr.GetOrdinal("baja"));
                                estadisticas["ABIERTO"] = dr.IsDBNull(dr.GetOrdinal("abierto")) ? 0 : dr.GetInt32(dr.GetOrdinal("abierto"));
                                estadisticas["CERRADO"] = dr.IsDBNull(dr.GetOrdinal("cerrado")) ? 0 : dr.GetInt32(dr.GetOrdinal("cerrado"));
                            }
                        }
                    }
                }
            }
            catch (PostgresException ex) when (EsRelacionNoExiste(ex))
            {
                return estadisticas;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerEstadisticas: {ex.Message}");
            }

            return estadisticas;
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
                    CREATE TABLE IF NOT EXISTS public.aocr_tbhallazgo
                    (
                        codigo_hallazgo SERIAL PRIMARY KEY,
                        codigo_inspeccion INTEGER NOT NULL,
                        descripcion TEXT,
                        criticidad VARCHAR(30),
                        estado VARCHAR(40),
                        accion_correctiva TEXT,
                        fecha_deteccion TIMESTAMP,
                        fecha_cierre TIMESTAMP,
                        responsable VARCHAR(200),
                        created_at TIMESTAMP,
                        created_by VARCHAR(100),
                        updated_at TIMESTAMP,
                        updated_by VARCHAR(100)
                    );

                    CREATE INDEX IF NOT EXISTS idx_hallazgo_inspeccion
                        ON public.aocr_tbhallazgo(codigo_inspeccion, fecha_deteccion DESC);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                const string alterSql = @"
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS descripcion TEXT;
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS criticidad VARCHAR(30);
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS estado VARCHAR(40);
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS accion_correctiva TEXT;
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS fecha_deteccion TIMESTAMP;
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS fecha_cierre TIMESTAMP;
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS responsable VARCHAR(200);
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS created_at TIMESTAMP;
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS created_by VARCHAR(100);
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP;
                    ALTER TABLE public.aocr_tbhallazgo ADD COLUMN IF NOT EXISTS updated_by VARCHAR(100);";

                using (var cmd = new NpgsqlCommand(alterSql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }
    }
}
