using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using CapaModelo;

namespace CapaDatos.Repositories
{
    public class DocumentoRepository : IDocumentoRepository
    {
        private readonly string _connectionString;

        public DocumentoRepository()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        }

        public int Crear(Documento documento)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var query = @"INSERT INTO Documentos 
                            (CodigoSolicitud, TipoDocumento, NombreArchivo, RutaGuardada, 
                             Extension, TamanoBytes, Estado, Validado, FechaCarga, 
                             Observaciones, Version, UsuarioRegistro)
                            OUTPUT INSERTED.CodigoDocumento
                            VALUES 
                            (@CodigoSolicitud, @TipoDocumento, @NombreArchivo, @RutaGuardada,
                             @Extension, @TamanoBytes, @Estado, @Validado, @FechaCarga,
                             @Observaciones, @Version, @UsuarioRegistro)";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CodigoSolicitud", documento.CodigoSolicitud);
                    command.Parameters.AddWithValue("@TipoDocumento", documento.TipoDocumento ?? "");
                    command.Parameters.AddWithValue("@NombreArchivo", documento.NombreArchivo ?? "");
                    command.Parameters.AddWithValue("@RutaGuardada", documento.RutaGuardada ?? "");
                    command.Parameters.AddWithValue("@Extension", documento.Extension ?? "");
                    command.Parameters.AddWithValue("@TamanoBytes", (object)documento.TamanoBytes ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Estado", documento.Estado ?? "PENDIENTE");
                    command.Parameters.AddWithValue("@Validado", (object)documento.Validado ?? DBNull.Value);
                    command.Parameters.AddWithValue("@FechaCarga", (object)documento.FechaCarga ?? DateTime.Now);
                    command.Parameters.AddWithValue("@Observaciones", (object)documento.Observaciones ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Version", (object)documento.Version ?? 1);
                    command.Parameters.AddWithValue("@UsuarioRegistro", documento.UsuarioRegistro ?? "");

                    connection.Open();
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public bool Actualizar(Documento documento)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var query = @"UPDATE Documentos SET
                            TipoDocumento = @TipoDocumento,
                            NombreArchivo = @NombreArchivo,
                            RutaGuardada = @RutaGuardada,
                            Extension = @Extension,
                            TamanoBytes = @TamanoBytes,
                            Estado = @Estado,
                            Validado = @Validado,
                            Observaciones = @Observaciones,
                            Version = @Version
                            WHERE CodigoDocumento = @CodigoDocumento";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CodigoDocumento", documento.CodigoDocumento);
                    command.Parameters.AddWithValue("@TipoDocumento", documento.TipoDocumento);
                    command.Parameters.AddWithValue("@NombreArchivo", documento.NombreArchivo);
                    command.Parameters.AddWithValue("@RutaGuardada", documento.RutaGuardada);
                    command.Parameters.AddWithValue("@Extension", documento.Extension);
                    command.Parameters.AddWithValue("@TamanoBytes", (object)documento.TamanoBytes ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Estado", documento.Estado);
                    command.Parameters.AddWithValue("@Validado", (object)documento.Validado ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Observaciones", (object)documento.Observaciones ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Version", documento.Version ?? 1);

                    connection.Open();
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool Eliminar(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var query = "DELETE FROM Documentos WHERE CodigoDocumento = @Id";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    connection.Open();
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        public Documento ObtenerPorId(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var query = "SELECT * FROM Documentos WHERE CodigoDocumento = @Id";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return MapearDocumentoDesdeReader(reader);
                        }
                        return null;
                    }
                }
            }
        }

        public List<Documento> ObtenerPorSolicitud(int solicitudId)
        {
            var lista = new List<Documento>();

            using (var connection = new SqlConnection(_connectionString))
            {
                var query = "SELECT * FROM Documentos WHERE CodigoSolicitud = @SolicitudId ORDER BY FechaCarga DESC";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SolicitudId", solicitudId);
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(MapearDocumentoDesdeReader(reader));
                        }
                    }
                }
            }
            return lista;
        }

        public List<Documento> ObtenerPorTipo(int solicitudId, string tipoDocumento)
        {
            var lista = new List<Documento>();

            using (var connection = new SqlConnection(_connectionString))
            {
                var query = @"SELECT * FROM Documentos 
                            WHERE CodigoSolicitud = @SolicitudId 
                            AND TipoDocumento = @TipoDocumento 
                            ORDER BY FechaCarga DESC";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SolicitudId", solicitudId);
                    command.Parameters.AddWithValue("@TipoDocumento", tipoDocumento);
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(MapearDocumentoDesdeReader(reader));
                        }
                    }
                }
            }
            return lista;
        }

        public List<Documento> ObtenerSubsanaciones(int solicitudId)
        {
            var lista = new List<Documento>();

            using (var connection = new SqlConnection(_connectionString))
            {
                var query = @"SELECT * FROM Documentos 
                            WHERE CodigoSolicitud = @SolicitudId 
                            AND (Observaciones LIKE '%subsan%' OR Estado = 'SUBSANACION')
                            ORDER BY FechaCarga DESC";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SolicitudId", solicitudId);
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(MapearDocumentoDesdeReader(reader));
                        }
                    }
                }
            }
            return lista;
        }

        public List<Documento> ObtenerPorEstado(int solicitudId, string estado)
        {
            var lista = new List<Documento>();

            using (var connection = new SqlConnection(_connectionString))
            {
                var query = @"SELECT * FROM Documentos 
                            WHERE CodigoSolicitud = @SolicitudId 
                            AND Estado = @Estado
                            ORDER BY FechaCarga DESC";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SolicitudId", solicitudId);
                    command.Parameters.AddWithValue("@Estado", estado);
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(MapearDocumentoDesdeReader(reader));
                        }
                    }
                }
            }
            return lista;
        }

        public bool ValidarDocumento(int documentoId, bool validado, string observaciones, string usuario)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var query = @"UPDATE Documentos SET
                            Validado = @Validado,
                            Estado = CASE WHEN @Validado = 1 THEN 'VALIDADO' ELSE 'RECHAZADO' END,
                            Observaciones = @Observaciones,
                            Version = ISNULL(Version, 0) + 1
                            WHERE CodigoDocumento = @DocumentoId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@DocumentoId", documentoId);
                    command.Parameters.AddWithValue("@Validado", validado);
                    command.Parameters.AddWithValue("@Observaciones", observaciones ?? "");

                    connection.Open();
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        private Documento MapearDocumentoDesdeReader(SqlDataReader reader)
        {
            return new Documento
            {
                CodigoDocumento = Convert.ToInt32(reader["CodigoDocumento"]),
                CodigoSolicitud = Convert.ToInt32(reader["CodigoSolicitud"]),
                TipoDocumento = reader["TipoDocumento"].ToString(),
                NombreArchivo = reader["NombreArchivo"].ToString(),
                RutaGuardada = reader["RutaGuardada"].ToString(),
                Extension = reader["Extension"].ToString(),
                TamanoBytes = reader["TamanoBytes"] as long?,
                Estado = reader["Estado"].ToString(),
                Validado = reader["Validado"] as bool?,
                FechaCarga = reader["FechaCarga"] as DateTime?,
                Observaciones = reader["Observaciones"] as string,
                Version = reader["Version"] as int?,
                UsuarioRegistro = reader["UsuarioRegistro"].ToString()
            };
        }
    }
}