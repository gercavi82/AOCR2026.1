using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using CapaModelo;

namespace CapaDatos.Repositories
{
    public class SolicitudRepository : ISolicitudRepository
    {
        private readonly string _connectionString;

        public SolicitudRepository()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        }

        public int Crear(SolicitudAOCR solicitud)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var query = @"INSERT INTO SolicitudesAOCR 
                            (UsuarioId, Estado, FechaCreacion, Observaciones, FechaEnvioRevision, 
                             FechaRevision, FechaSubsanacion, FechaAprobacion, UsuarioRevisionId, 
                             UsuarioAprobacionId)
                            OUTPUT INSERTED.Id
                            VALUES 
                            (@UsuarioId, @Estado, @FechaCreacion, @Observaciones, @FechaEnvioRevision,
                             @FechaRevision, @FechaSubsanacion, @FechaAprobacion, @UsuarioRevisionId,
                             @UsuarioAprobacionId)";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UsuarioId", solicitud.UsuarioId);
                    command.Parameters.AddWithValue("@Estado", solicitud.Estado ?? "BORRADOR");
                    command.Parameters.AddWithValue("@FechaCreacion", solicitud.FechaCreacion);
                    command.Parameters.AddWithValue("@Observaciones", (object)solicitud.Observaciones ?? DBNull.Value);
                    command.Parameters.AddWithValue("@FechaEnvioRevision", (object)solicitud.FechaEnvioRevision ?? DBNull.Value);
                    command.Parameters.AddWithValue("@FechaRevision", (object)solicitud.FechaRevision ?? DBNull.Value);
                    command.Parameters.AddWithValue("@FechaSubsanacion", (object)solicitud.FechaSubsanacion ?? DBNull.Value);
                    command.Parameters.AddWithValue("@FechaAprobacion", (object)solicitud.FechaAprobacion ?? DBNull.Value);
                    command.Parameters.AddWithValue("@UsuarioRevisionId", (object)solicitud.UsuarioRevisionId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@UsuarioAprobacionId", (object)solicitud.UsuarioAprobacionId ?? DBNull.Value);

                    connection.Open();
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public bool Actualizar(SolicitudAOCR solicitud)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var query = @"UPDATE SolicitudesAOCR SET
                            Estado = @Estado,
                            Observaciones = @Observaciones,
                            FechaEnvioRevision = @FechaEnvioRevision,
                            FechaRevision = @FechaRevision,
                            FechaSubsanacion = @FechaSubsanacion,
                            FechaAprobacion = @FechaAprobacion,
                            UsuarioRevisionId = @UsuarioRevisionId,
                            UsuarioAprobacionId = @UsuarioAprobacionId
                            WHERE Id = @Id";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", solicitud.Id);
                    command.Parameters.AddWithValue("@Estado", solicitud.Estado);
                    command.Parameters.AddWithValue("@Observaciones", (object)solicitud.Observaciones ?? DBNull.Value);
                    command.Parameters.AddWithValue("@FechaEnvioRevision", (object)solicitud.FechaEnvioRevision ?? DBNull.Value);
                    command.Parameters.AddWithValue("@FechaRevision", (object)solicitud.FechaRevision ?? DBNull.Value);
                    command.Parameters.AddWithValue("@FechaSubsanacion", (object)solicitud.FechaSubsanacion ?? DBNull.Value);
                    command.Parameters.AddWithValue("@FechaAprobacion", (object)solicitud.FechaAprobacion ?? DBNull.Value);
                    command.Parameters.AddWithValue("@UsuarioRevisionId", (object)solicitud.UsuarioRevisionId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@UsuarioAprobacionId", (object)solicitud.UsuarioAprobacionId ?? DBNull.Value);

                    connection.Open();
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        public SolicitudAOCR ObtenerPorId(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var query = "SELECT * FROM SolicitudesAOCR WHERE Id = @Id";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new SolicitudAOCR
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                UsuarioId = Convert.ToInt32(reader["UsuarioId"]),
                                Estado = reader["Estado"].ToString(),
                                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                                Observaciones = reader["Observaciones"] as string,
                                FechaEnvioRevision = reader["FechaEnvioRevision"] as DateTime?,
                                FechaRevision = reader["FechaRevision"] as DateTime?,
                                FechaSubsanacion = reader["FechaSubsanacion"] as DateTime?,
                                FechaAprobacion = reader["FechaAprobacion"] as DateTime?,
                                UsuarioRevisionId = reader["UsuarioRevisionId"] as int?,
                                UsuarioAprobacionId = reader["UsuarioAprobacionId"] as int?
                            };
                        }
                        return null;
                    }
                }
            }
        }

        public List<SolicitudAOCR> ObtenerPorUsuario(int usuarioId)
        {
            var lista = new List<SolicitudAOCR>();

            using (var connection = new SqlConnection(_connectionString))
            {
                var query = "SELECT * FROM SolicitudesAOCR WHERE UsuarioId = @UsuarioId ORDER BY FechaCreacion DESC";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new SolicitudAOCR
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                UsuarioId = Convert.ToInt32(reader["UsuarioId"]),
                                Estado = reader["Estado"].ToString(),
                                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                                Observaciones = reader["Observaciones"] as string,
                                FechaEnvioRevision = reader["FechaEnvioRevision"] as DateTime?,
                                FechaRevision = reader["FechaRevision"] as DateTime?,
                                FechaSubsanacion = reader["FechaSubsanacion"] as DateTime?,
                                FechaAprobacion = reader["FechaAprobacion"] as DateTime?,
                                UsuarioRevisionId = reader["UsuarioRevisionId"] as int?,
                                UsuarioAprobacionId = reader["UsuarioAprobacionId"] as int?
                            });
                        }
                    }
                }
            }
            return lista;
        }

        public List<object> ObtenerHistorialEstados(int solicitudId)
        {
            var historial = new List<object>();

            using (var connection = new SqlConnection(_connectionString))
            {
                var query = @"SELECT Estado, FechaCreacion as Fecha FROM SolicitudesAOCR WHERE Id = @Id
                            UNION
                            SELECT 'EN_REVISION_DOCUMENTAL' as Estado, FechaEnvioRevision as Fecha 
                            FROM SolicitudesAOCR WHERE Id = @Id AND FechaEnvioRevision IS NOT NULL
                            UNION
                            SELECT 'SUBSANACION' as Estado, FechaRevision as Fecha 
                            FROM SolicitudesAOCR WHERE Id = @Id AND FechaRevision IS NOT NULL
                            UNION
                            SELECT 'DOCUMENTACION_APROBADA' as Estado, FechaAprobacion as Fecha 
                            FROM SolicitudesAOCR WHERE Id = @Id AND FechaAprobacion IS NOT NULL
                            ORDER BY Fecha";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", solicitudId);
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            historial.Add(new
                            {
                                Estado = reader["Estado"].ToString(),
                                Fecha = Convert.ToDateTime(reader["Fecha"])
                            });
                        }
                    }
                }
            }
            return historial;
        }

        // Métodos restantes (Implementar según necesidad)
        public bool Eliminar(int id) { /* implementación */ return false; }
        public List<SolicitudAOCR> ObtenerTodas() { /* implementación */ return new List<SolicitudAOCR>(); }
        public List<SolicitudAOCR> ObtenerPorEstado(string estado) { /* implementación */ return new List<SolicitudAOCR>(); }
    }
}