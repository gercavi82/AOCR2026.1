using System;
using System.Collections.Generic;
using System.Configuration;
using Npgsql;
using CapaModelo;

namespace CapaDatos.Repositories
{
    public class SolicitudRepository : ISolicitudRepository
    {
        private readonly string _connectionString;

        public SolicitudRepository()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;
        }

        public int Crear(SolicitudAOCR solicitud)
        {
            if (solicitud == null) throw new ArgumentNullException(nameof(solicitud));

            // Ajustado a tu tabla aocr_tbsolicitud (Postgres)
            const string sql = @"
INSERT INTO aocr_tbsolicitud
(
  numero_solicitud,
  fecha_solicitud,
  tipo_solicitud,
  estado,
  nombre_operador,
  ruc,
  razon_social,
  email,
  telefono,
  direccion,
  ciudad,
  provincia,
  pais,
  representante_legal,
  cedula_representante,
  tipo_operacion,
  descripcion_operacion,
  observaciones,
  codigo_usuario,
  created_at,
  created_by
)
VALUES
(
  @numero_solicitud,
  @fecha_solicitud,
  @tipo_solicitud,
  @estado,
  @nombre_operador,
  @ruc,
  @razon_social,
  @email,
  @telefono,
  @direccion,
  @ciudad,
  @provincia,
  COALESCE(@pais, 'Ecuador'),
  @representante_legal,
  @cedula_representante,
  @tipo_operacion,
  @descripcion_operacion,
  @observaciones,
  @codigo_usuario,
  now(),
  @created_by
)
RETURNING codigo_solicitud;";

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                // Nota: adapta estas asignaciones a tu clase SolicitudAOCR real.
                cmd.Parameters.AddWithValue("@numero_solicitud", (object)(solicitud.NumeroSolicitud ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fecha_solicitud", solicitud.FechaCreacion != default(DateTime) ? (object)solicitud.FechaCreacion : DateTime.Now);

                cmd.Parameters.AddWithValue("@tipo_solicitud", solicitud.TipoSolicitud); // int (según tu tabla)
                cmd.Parameters.AddWithValue("@estado", (object)(solicitud.Estado ?? "Pendiente") ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@nombre_operador", (object)(solicitud.NombreOperador ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruc", string.IsNullOrWhiteSpace(solicitud.Ruc) ? (object)DBNull.Value : solicitud.Ruc);
                cmd.Parameters.AddWithValue("@razon_social", string.IsNullOrWhiteSpace(solicitud.RazonSocial) ? (object)DBNull.Value : solicitud.RazonSocial);
                cmd.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(solicitud.Email) ? (object)DBNull.Value : solicitud.Email);
                cmd.Parameters.AddWithValue("@telefono", string.IsNullOrWhiteSpace(solicitud.Telefono) ? (object)DBNull.Value : solicitud.Telefono);
                cmd.Parameters.AddWithValue("@direccion", string.IsNullOrWhiteSpace(solicitud.Direccion) ? (object)DBNull.Value : solicitud.Direccion);
                cmd.Parameters.AddWithValue("@ciudad", string.IsNullOrWhiteSpace(solicitud.Ciudad) ? (object)DBNull.Value : solicitud.Ciudad);
                cmd.Parameters.AddWithValue("@provincia", string.IsNullOrWhiteSpace(solicitud.Provincia) ? (object)DBNull.Value : solicitud.Provincia);
                cmd.Parameters.AddWithValue("@pais", string.IsNullOrWhiteSpace(solicitud.Pais) ? (object)DBNull.Value : solicitud.Pais);

                cmd.Parameters.AddWithValue("@representante_legal", string.IsNullOrWhiteSpace(solicitud.RepresentanteLegal) ? (object)DBNull.Value : solicitud.RepresentanteLegal);
                cmd.Parameters.AddWithValue("@cedula_representante", string.IsNullOrWhiteSpace(solicitud.CedulaRepresentante) ? (object)DBNull.Value : solicitud.CedulaRepresentante);

                cmd.Parameters.AddWithValue("@tipo_operacion", (object)(solicitud.TipoOperacion ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@descripcion_operacion", string.IsNullOrWhiteSpace(solicitud.DescripcionOperacion) ? (object)DBNull.Value : solicitud.DescripcionOperacion);
                cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrWhiteSpace(solicitud.Observaciones) ? (object)DBNull.Value : solicitud.Observaciones);

                // En tu tabla existe codigo_usuario (int)
                cmd.Parameters.AddWithValue("@codigo_usuario", solicitud.UsuarioId);

                // created_by (texto)
                cmd.Parameters.AddWithValue("@created_by", string.IsNullOrWhiteSpace(solicitud.UsuarioRegistro) ? (object)DBNull.Value : solicitud.UsuarioRegistro);

                cn.Open();
                var id = cmd.ExecuteScalar();
                return Convert.ToInt32(id);
            }
        }

        public bool Actualizar(SolicitudAOCR solicitud)
        {
            if (solicitud == null) throw new ArgumentNullException(nameof(solicitud));

            const string sql = @"
UPDATE aocr_tbsolicitud SET
  tipo_solicitud = @tipo_solicitud,
  estado = @estado,
  nombre_operador = @nombre_operador,
  ruc = @ruc,
  razon_social = @razon_social,
  email = @email,
  telefono = @telefono,
  direccion = @direccion,
  ciudad = @ciudad,
  provincia = @provincia,
  pais = COALESCE(@pais, pais),
  representante_legal = @representante_legal,
  cedula_representante = @cedula_representante,
  tipo_operacion = @tipo_operacion,
  descripcion_operacion = @descripcion_operacion,
  observaciones = @observaciones,
  updated_at = now(),
  updated_by = @updated_by
WHERE codigo_solicitud = @codigo_solicitud;";

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_solicitud", solicitud.Id);

                cmd.Parameters.AddWithValue("@tipo_solicitud", solicitud.TipoSolicitud);
                cmd.Parameters.AddWithValue("@estado", (object)(solicitud.Estado ?? "Pendiente") ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@nombre_operador", (object)(solicitud.NombreOperador ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruc", string.IsNullOrWhiteSpace(solicitud.Ruc) ? (object)DBNull.Value : solicitud.Ruc);
                cmd.Parameters.AddWithValue("@razon_social", string.IsNullOrWhiteSpace(solicitud.RazonSocial) ? (object)DBNull.Value : solicitud.RazonSocial);
                cmd.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(solicitud.Email) ? (object)DBNull.Value : solicitud.Email);
                cmd.Parameters.AddWithValue("@telefono", string.IsNullOrWhiteSpace(solicitud.Telefono) ? (object)DBNull.Value : solicitud.Telefono);
                cmd.Parameters.AddWithValue("@direccion", string.IsNullOrWhiteSpace(solicitud.Direccion) ? (object)DBNull.Value : solicitud.Direccion);
                cmd.Parameters.AddWithValue("@ciudad", string.IsNullOrWhiteSpace(solicitud.Ciudad) ? (object)DBNull.Value : solicitud.Ciudad);
                cmd.Parameters.AddWithValue("@provincia", string.IsNullOrWhiteSpace(solicitud.Provincia) ? (object)DBNull.Value : solicitud.Provincia);
                cmd.Parameters.AddWithValue("@pais", string.IsNullOrWhiteSpace(solicitud.Pais) ? (object)DBNull.Value : solicitud.Pais);

                cmd.Parameters.AddWithValue("@representante_legal", string.IsNullOrWhiteSpace(solicitud.RepresentanteLegal) ? (object)DBNull.Value : solicitud.RepresentanteLegal);
                cmd.Parameters.AddWithValue("@cedula_representante", string.IsNullOrWhiteSpace(solicitud.CedulaRepresentante) ? (object)DBNull.Value : solicitud.CedulaRepresentante);

                cmd.Parameters.AddWithValue("@tipo_operacion", (object)(solicitud.TipoOperacion ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@descripcion_operacion", string.IsNullOrWhiteSpace(solicitud.DescripcionOperacion) ? (object)DBNull.Value : solicitud.DescripcionOperacion);
                cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrWhiteSpace(solicitud.Observaciones) ? (object)DBNull.Value : solicitud.Observaciones);

                cmd.Parameters.AddWithValue("@updated_by", string.IsNullOrWhiteSpace(solicitud.UsuarioActualiza) ? (object)DBNull.Value : solicitud.UsuarioActualiza);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public SolicitudAOCR ObtenerPorId(int id)
        {
            const string sql = @"
SELECT
  codigo_solicitud,
  numero_solicitud,
  fecha_solicitud,
  tipo_solicitud,
  estado,
  nombre_operador,
  ruc,
  razon_social,
  email,
  telefono,
  direccion,
  ciudad,
  provincia,
  pais,
  representante_legal,
  cedula_representante,
  tipo_operacion,
  descripcion_operacion,
  observaciones,
  codigo_usuario,
  created_by
FROM aocr_tbsolicitud
WHERE codigo_solicitud = @id;";

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return MapearSolicitud(r);
                }
            }
        }

        public List<SolicitudAOCR> ObtenerPorUsuario(int usuarioId)
        {
            const string sql = @"
SELECT
  codigo_solicitud,
  numero_solicitud,
  fecha_solicitud,
  tipo_solicitud,
  estado,
  nombre_operador,
  ruc,
  razon_social,
  email,
  telefono,
  direccion,
  ciudad,
  provincia,
  pais,
  representante_legal,
  cedula_representante,
  tipo_operacion,
  descripcion_operacion,
  observaciones,
  codigo_usuario,
  created_by
FROM aocr_tbsolicitud
WHERE codigo_usuario = @usuarioId
ORDER BY fecha_solicitud DESC;";

            var lista = new List<SolicitudAOCR>();

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@usuarioId", usuarioId);
                cn.Open();

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        lista.Add(MapearSolicitud(r));
                }
            }

            return lista;
        }

        public List<object> ObtenerHistorialEstados(int solicitudId)
        {
            // En tu esquema existe aocr_tbhistorial_estado para solicitudes.
            // Si tu sistema ya registra cambios de estado ahí, usa esa tabla (recomendado).
            // Si NO existe registro real aún, este método simula eventos con fecha_solicitud/created_at/updated_at.
            const string sql = @"
SELECT estado_nuevo AS estado, fecha_cambio AS fecha
FROM aocr_tbhistorial_estado
WHERE codigo_solicitud = @id
UNION ALL
SELECT estado, fecha_solicitud AS fecha
FROM aocr_tbsolicitud
WHERE codigo_solicitud = @id
ORDER BY fecha;";

            var historial = new List<object>();

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", solicitudId);
                cn.Open();

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        historial.Add(new
                        {
                            Estado = r["estado"]?.ToString(),
                            Fecha = r["fecha"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["fecha"])
                        });
                    }
                }
            }

            return historial;
        }

        // === Pendientes / según tu necesidad ===

        public bool Eliminar(int id)
        {
            // Recomendado: soft delete si tienes deleted_at/deleted_by
            const string sql = @"
UPDATE aocr_tbsolicitud
SET deleted_at = now(), deleted_by = @user
WHERE codigo_solicitud = @id;";

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@user", "SYSTEM"); // cámbialo por el usuario real
                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public List<SolicitudAOCR> ObtenerTodas()
        {
            const string sql = @"
SELECT
  codigo_solicitud,
  numero_solicitud,
  fecha_solicitud,
  tipo_solicitud,
  estado,
  nombre_operador,
  ruc,
  razon_social,
  email,
  telefono,
  direccion,
  ciudad,
  provincia,
  pais,
  representante_legal,
  cedula_representante,
  tipo_operacion,
  descripcion_operacion,
  observaciones,
  codigo_usuario,
  created_by
FROM aocr_tbsolicitud
WHERE deleted_at IS NULL
ORDER BY fecha_solicitud DESC;";

            var lista = new List<SolicitudAOCR>();

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        lista.Add(MapearSolicitud(r));
                }
            }

            return lista;
        }

        public List<SolicitudAOCR> ObtenerPorEstado(string estado)
        {
            const string sql = @"
SELECT
  codigo_solicitud,
  numero_solicitud,
  fecha_solicitud,
  tipo_solicitud,
  estado,
  nombre_operador,
  ruc,
  razon_social,
  email,
  telefono,
  direccion,
  ciudad,
  provincia,
  pais,
  representante_legal,
  cedula_representante,
  tipo_operacion,
  descripcion_operacion,
  observaciones,
  codigo_usuario,
  created_by
FROM aocr_tbsolicitud
WHERE estado = @estado
  AND deleted_at IS NULL
ORDER BY fecha_solicitud DESC;";

            var lista = new List<SolicitudAOCR>();

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@estado", estado ?? "");
                cn.Open();

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        lista.Add(MapearSolicitud(r));
                }
            }

            return lista;
        }

        // === Mapper robusto (NULL-safe) ===
        private SolicitudAOCR MapearSolicitud(NpgsqlDataReader r)
        {
            return new SolicitudAOCR
            {
                // Ajusta si tu clase usa otros nombres
                Id = Convert.ToInt32(r["codigo_solicitud"]),
                NumeroSolicitud = r["numero_solicitud"] as string,
                FechaCreacion = r["fecha_solicitud"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(r["fecha_solicitud"]),
                TipoSolicitud = r["tipo_solicitud"] == DBNull.Value ? 0 : Convert.ToInt32(r["tipo_solicitud"]),
                Estado = r["estado"] as string,

                NombreOperador = r["nombre_operador"] as string,
                Ruc = r["ruc"] as string,
                RazonSocial = r["razon_social"] as string,
                Email = r["email"] as string,
                Telefono = r["telefono"] as string,
                Direccion = r["direccion"] == DBNull.Value ? null : (string)r["direccion"],
                Ciudad = r["ciudad"] as string,
                Provincia = r["provincia"] as string,
                Pais = r["pais"] as string,

                RepresentanteLegal = r["representante_legal"] as string,
                CedulaRepresentante = r["cedula_representante"] as string,

                TipoOperacion = r["tipo_operacion"] as string,
                DescripcionOperacion = r["descripcion_operacion"] == DBNull.Value ? null : (string)r["descripcion_operacion"],
                Observaciones = r["observaciones"] == DBNull.Value ? null : (string)r["observaciones"],

                UsuarioId = r["codigo_usuario"] == DBNull.Value ? 0 : Convert.ToInt32(r["codigo_usuario"]),
                UsuarioRegistro = r["created_by"] == DBNull.Value ? null : (string)r["created_by"]
            };
        }
    }
}
