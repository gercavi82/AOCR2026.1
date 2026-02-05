using System;
using System.Collections.Generic;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class SolicitudDAO
    {
        private readonly string _cn;

        public SolicitudDAO()
        {
            _cn = System.Configuration.ConfigurationManager
                .ConnectionStrings["AOCRConnection"]
                .ConnectionString;
        }

        public List<SolicitudAOCR> ObtenerTodas(string estado = null)
        {
            var lista = new List<SolicitudAOCR>();

            var sql = @"
SELECT codigo_solicitud, numero_solicitud, fecha_solicitud, tipo_solicitud, estado,
       nombre_operador, ruc, razon_social, email, telefono, direccion,
       ciudad, provincia, pais, representante_legal, cedula_representante,
       tipo_operacion, descripcion_operacion, observaciones,
       codigo_usuario, codigo_tecnico,
       created_at, updated_at, created_by, updated_by,
       deleted_at, deleted_by
FROM aocr_tbsolicitud";

            if (!string.IsNullOrWhiteSpace(estado))
                sql += " WHERE UPPER(TRIM(estado)) = @estado";

            sql += " ORDER BY fecha_solicitud DESC";

            using (var cn = new NpgsqlConnection(_cn))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                if (!string.IsNullOrWhiteSpace(estado))
                    cmd.Parameters.AddWithValue("@estado", estado.Trim().ToUpperInvariant());

                cn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        lista.Add(new SolicitudAOCR
                        {
                            CodigoSolicitud = dr.GetInt32(0),
                            NumeroSolicitud = dr.IsDBNull(1) ? null : dr.GetString(1),
                            FechaSolicitud = dr.IsDBNull(2) ? (DateTime?)null : dr.GetDateTime(2),
                            TipoSolicitud = dr.IsDBNull(3) ? (int?)null : dr.GetInt32(3),
                            Estado = dr.IsDBNull(4) ? null : dr.GetString(4),
                            NombreOperador = dr.IsDBNull(5) ? null : dr.GetString(5),
                            Ruc = dr.IsDBNull(6) ? null : dr.GetString(6),
                            RazonSocial = dr.IsDBNull(7) ? null : dr.GetString(7),
                            Email = dr.IsDBNull(8) ? null : dr.GetString(8),
                            Telefono = dr.IsDBNull(9) ? null : dr.GetString(9),
                            Direccion = dr.IsDBNull(10) ? null : dr.GetString(10),
                            Ciudad = dr.IsDBNull(11) ? null : dr.GetString(11),
                            Provincia = dr.IsDBNull(12) ? null : dr.GetString(12),
                            Pais = dr.IsDBNull(13) ? null : dr.GetString(13),
                            RepresentanteLegal = dr.IsDBNull(14) ? null : dr.GetString(14),
                            CedulaRepresentante = dr.IsDBNull(15) ? null : dr.GetString(15),
                            TipoOperacion = dr.IsDBNull(16) ? null : dr.GetString(16),
                            DescripcionOperacion = dr.IsDBNull(17) ? null : dr.GetString(17),
                            Observaciones = dr.IsDBNull(18) ? null : dr.GetString(18),
                            CodigoUsuario = dr.IsDBNull(19) ? 0 : dr.GetInt32(19),
                            CodigoTecnico = dr.IsDBNull(20) ? (int?)null : dr.GetInt32(20),
                            CreatedAt = dr.IsDBNull(21) ? (DateTime?)null : dr.GetDateTime(21),
                            UpdatedAt = dr.IsDBNull(22) ? (DateTime?)null : dr.GetDateTime(22),
                            CreatedBy = dr.IsDBNull(23) ? null : dr.GetString(23),
                            UpdatedBy = dr.IsDBNull(24) ? null : dr.GetString(24),
                            DeletedAt = dr.IsDBNull(25) ? (DateTime?)null : dr.GetDateTime(25),
                            DeletedBy = dr.IsDBNull(26) ? null : dr.GetString(26),
                            FechaInicioOperacion = null,
                            FechaFinOperacion = null,
                            ObservacionesGenerales = null
                        });
                    }
                }
            }

            return lista;
        }

        // Obtener una solicitud por su Id (codigo_solicitud PK)
        public SolicitudAOCR ObtenerPorId(int id)
        {
            var sql = @"
SELECT codigo_solicitud, numero_solicitud, fecha_solicitud, tipo_solicitud, estado,
       nombre_operador, ruc, razon_social, email, telefono, direccion,
       ciudad, provincia, pais, representante_legal, cedula_representante,
       tipo_operacion, descripcion_operacion, observaciones,
       codigo_usuario, codigo_tecnico,
       created_at, updated_at, created_by, updated_by,
       deleted_at, deleted_by
FROM aocr_tbsolicitud
WHERE codigo_solicitud = @id";

            using (var cn = new NpgsqlConnection(_cn))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) return null;

                    return new SolicitudAOCR
                    {
                        CodigoSolicitud = dr.GetInt32(0),
                        NumeroSolicitud = dr.IsDBNull(1) ? null : dr.GetString(1),
                        FechaSolicitud = dr.IsDBNull(2) ? (DateTime?)null : dr.GetDateTime(2),
                        TipoSolicitud = dr.IsDBNull(3) ? (int?)null : dr.GetInt32(3),
                        Estado = dr.IsDBNull(4) ? null : dr.GetString(4),
                        NombreOperador = dr.IsDBNull(5) ? null : dr.GetString(5),
                        Ruc = dr.IsDBNull(6) ? null : dr.GetString(6),
                        RazonSocial = dr.IsDBNull(7) ? null : dr.GetString(7),
                        Email = dr.IsDBNull(8) ? null : dr.GetString(8),
                        Telefono = dr.IsDBNull(9) ? null : dr.GetString(9),
                        Direccion = dr.IsDBNull(10) ? null : dr.GetString(10),
                        Ciudad = dr.IsDBNull(11) ? null : dr.GetString(11),
                        Provincia = dr.IsDBNull(12) ? null : dr.GetString(12),
                        Pais = dr.IsDBNull(13) ? null : dr.GetString(13),
                        RepresentanteLegal = dr.IsDBNull(14) ? null : dr.GetString(14),
                        CedulaRepresentante = dr.IsDBNull(15) ? null : dr.GetString(15),
                        TipoOperacion = dr.IsDBNull(16) ? null : dr.GetString(16),
                        DescripcionOperacion = dr.IsDBNull(17) ? null : dr.GetString(17),
                        Observaciones = dr.IsDBNull(18) ? null : dr.GetString(18),
                        CodigoUsuario = dr.IsDBNull(19) ? 0 : dr.GetInt32(19),
                        CodigoTecnico = dr.IsDBNull(20) ? (int?)null : dr.GetInt32(20),
                        CreatedAt = dr.IsDBNull(21) ? (DateTime?)null : dr.GetDateTime(21),
                        UpdatedAt = dr.IsDBNull(22) ? (DateTime?)null : dr.GetDateTime(22),
                        CreatedBy = dr.IsDBNull(23) ? null : dr.GetString(23),
                        UpdatedBy = dr.IsDBNull(24) ? null : dr.GetString(24),
                        DeletedAt = dr.IsDBNull(25) ? (DateTime?)null : dr.GetDateTime(25),
                        DeletedBy = dr.IsDBNull(26) ? null : dr.GetString(26),
                        FechaInicioOperacion = null,
                        FechaFinOperacion = null,
                        ObservacionesGenerales = null
                    };
                }
            }
        }

        // Actualizar estado y metadatos básicos de la solicitud
        public bool Actualizar(SolicitudAOCR solicitud)
        {
            const string sql = @"
UPDATE aocr_tbsolicitud
   SET estado = @estado,
       updated_at = @updated_at,
       updated_by = @updated_by
 WHERE codigo_solicitud = @id";

            using (var cn = new NpgsqlConnection(_cn))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@estado", (object)(solicitud.Estado ?? string.Empty));
                cmd.Parameters.AddWithValue("@updated_at", (object)(solicitud.UpdatedAt ?? DateTime.Now));
                cmd.Parameters.AddWithValue("@updated_by", (object)(solicitud.UpdatedBy ?? "SYSTEM"));
                cmd.Parameters.AddWithValue("@id", solicitud.CodigoSolicitud);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }
}
