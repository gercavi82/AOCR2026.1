using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaDatos.Constants;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class SolicitudAOCRDAO
    {
        private string ConnectionString =>
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        // ============================
        // LISTADOS
        // ============================
        public List<SolicitudAOCR> ListarActivas() => ObtenerPorFiltro("deleted_at IS NULL");
        public List<SolicitudAOCR> ObtenerTodos() => ObtenerPorFiltro("1=1");

        public List<SolicitudAOCR> ObtenerPorUsuario(int codigoUsuario)
        {
            return ObtenerPorFiltro(
                "codigo_usuario = @u AND deleted_at IS NULL",
                cmd => cmd.Parameters.AddWithValue("@u", codigoUsuario)
            );
        }

        public List<SolicitudAOCR> ObtenerPorEstado(string estado)
        {
            return ObtenerPorFiltro(
                "estado = @e AND deleted_at IS NULL",
                cmd => cmd.Parameters.AddWithValue("@e", estado ?? "")
            );
        }

        // Múltiples estados a la vez
        public List<SolicitudAOCR> ObtenerPorEstados(params string[] estados)
        {
            if (estados == null || estados.Length == 0)
                return ObtenerTodos();

            var placeholders = new List<string>();
            for (int i = 0; i < estados.Length; i++)
                placeholders.Add($"@e{i}");

            string where = $"estado = ANY (ARRAY[{string.Join(",", placeholders)}]) AND deleted_at IS NULL";

            return ObtenerPorFiltro(where, cmd =>
            {
                for (int i = 0; i < estados.Length; i++)
                    cmd.Parameters.AddWithValue($"@e{i}", estados[i] ?? string.Empty);
            });
        }

        public List<SolicitudAOCR> ObtenerPendientesRevision() => ObtenerPorEstado("ENVIADO_A_INSPECTOR");

        public List<SolicitudAOCR> ObtenerParaValidacionJefatura()
        {
            return ObtenerPorFiltro(
                "estado = @e AND deleted_at IS NULL",
                cmd => cmd.Parameters.AddWithValue("@e", "ENVIADO_A_JEFATURA")
            );
        }

        public List<SolicitudAOCR> ObtenerPendientesAsignacion()
        {
            // Obtener solicitudes aprobadas o en estado de inspección que aún no tienen inspector asignado
            string sql = @"
                SELECT s.* 
                FROM aocr_tbsolicitud s
                LEFT JOIN aocr_tbinspeccion i ON s.codigo_solicitud = i.codigo_solicitud
                WHERE s.estado IN ('APROBADA', 'INSPECCION_SOLICITADA') 
                  AND s.deleted_at IS NULL
                  AND i.codigo_inspeccion IS NULL
                ORDER BY s.fecha_solicitud DESC";

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand(sql, cn))
                using (var rd = cmd.ExecuteReader())
                {
                    var lista = new List<SolicitudAOCR>();
                    while (rd.Read()) lista.Add(Mapear(rd));
                    return lista;
                }
            }
        }

        // ============================
        // OBTENER INDIVIDUAL
        // ============================
        public SolicitudAOCR ObtenerPorId(int id)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                const string sql = @"SELECT * FROM aocr_tbsolicitud
                                     WHERE codigo_solicitud = @id AND deleted_at IS NULL
                                     LIMIT 1";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Mapear(rd) : null;
                    }
                }
            }
        }

        // ✅ COMPATIBILIDAD
        public SolicitudAOCR ObtenerPorCodigo(int codigoSolicitud)
        {
            string sql = @"
        SELECT 
            codigo_solicitud,
            numero_solicitud,
            fecha_solicitud,
            nombre_operador,
            ruc,
            razon_social,
            email,
            telefono,
            direccion,
            representante_legal,
            cedula_representante,
            tipo_operacion,
            descripcion_operacion,
            observaciones,
            estado,
            codigo_usuario
        FROM aocr_tbsolicitud 
        WHERE codigo_solicitud = @CodigoSolicitud
        AND estado != 'Eliminado'";

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@CodigoSolicitud", codigoSolicitud);
                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Mapear(rd) : null;
                    }
                }
            }
        }

        // ============================
        // INSERTAR (COMPLETO)
        // ============================
        public int InsertarConReturn(SolicitudAOCR solicitud)
        {
            string sql = @"
        INSERT INTO aocr_tbsolicitud (
            numero_solicitud,
            fecha_solicitud,
            tipo_solicitud,
            nombre_operador,
            ruc,
            razon_social,
            email,
            telefono,
            direccion,
            representante_legal,
            cedula_representante,
            tipo_operacion,
            descripcion_operacion,
            observaciones,
            estado,
            codigo_usuario
        ) VALUES (
            @NumeroSolicitud,
            @FechaSolicitud,
            @TipoSolicitud,
            @NombreOperador,
            @Ruc,
            @RazonSocial,
            @Email,
            @Telefono,
            @Direccion,
            @RepresentanteLegal,
            @CedulaRepresentante,
            @TipoOperacion,
            @DescripcionOperacion,
            @Observaciones,
            @Estado,
            @CodigoUsuario
        ) RETURNING codigo_solicitud";

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@NumeroSolicitud", (object)(solicitud.NumeroSolicitud ?? ""));
                    cmd.Parameters.AddWithValue("@FechaSolicitud", (object)solicitud.FechaSolicitud ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TipoSolicitud", (object)(solicitud.TipoSolicitud ?? 1));
                    cmd.Parameters.AddWithValue("@NombreOperador", (object)(solicitud.NombreOperador ?? ""));
                    cmd.Parameters.AddWithValue("@Ruc", (object)(solicitud.Ruc ?? ""));
                    cmd.Parameters.AddWithValue("@RazonSocial", (object)(solicitud.RazonSocial ?? ""));

                    cmd.Parameters.AddWithValue("@Email", (object)(solicitud.Email ?? ""));
                    cmd.Parameters.AddWithValue("@Telefono", (object)(solicitud.Telefono ?? ""));
                    cmd.Parameters.AddWithValue("@Direccion", (object)(solicitud.Direccion ?? ""));
                    cmd.Parameters.AddWithValue("@RepresentanteLegal", (object)(solicitud.RepresentanteLegal ?? ""));
                    cmd.Parameters.AddWithValue("@CedulaRepresentante", (object)(solicitud.CedulaRepresentante ?? ""));

                    cmd.Parameters.AddWithValue("@TipoOperacion", (object)(solicitud.TipoOperacion ?? ""));
                    cmd.Parameters.AddWithValue("@DescripcionOperacion", (object)(solicitud.DescripcionOperacion ?? ""));
                    cmd.Parameters.AddWithValue("@Observaciones", (object)(solicitud.Observaciones ?? ""));

                    var estadoNormalizado = EstadoSolicitud.Normalizar(solicitud.Estado);
                    cmd.Parameters.AddWithValue("@Estado", estadoNormalizado);

                    cmd.Parameters.AddWithValue("@CodigoUsuario", solicitud.CodigoUsuario);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // ============================
        // ACTUALIZAR (COMPLETO)
        // ============================
        public bool ActualizarGeneral(SolicitudAOCR s)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                const string sql = @"
UPDATE aocr_tbsolicitud
SET
  numero_solicitud=@numero,
  fecha_solicitud=@fecha,
  tipo_solicitud=@tipo_solicitud,
  estado=@estado,

  nombre_operador=@nombre_operador,
  ruc=@ruc,
  razon_social=@razon_social,
  email=@email,
  telefono=@telefono,
  direccion=@direccion,
  ciudad=@ciudad,
  provincia=@provincia,
  pais=@pais,

  representante_legal=@representante_legal,
  cedula_representante=@cedula_representante,

  tipo_operacion=@tipo_operacion,
  descripcion_operacion=@descripcion_operacion,
  observaciones=@observaciones,

  codigo_tecnico=@codigo_tecnico,
  updated_at=NOW(),
  updated_by=@updated_by
WHERE codigo_solicitud=@id AND deleted_at IS NULL;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", s.CodigoSolicitud);

                    cmd.Parameters.AddWithValue("@numero", (object)(s.NumeroSolicitud ?? ""));
                    cmd.Parameters.AddWithValue("@fecha", (object)s.FechaSolicitud ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo_solicitud", (object)s.TipoSolicitud ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado", (object)(s.Estado ?? ""));

                    cmd.Parameters.AddWithValue("@nombre_operador", (object)(s.NombreOperador ?? ""));
                    cmd.Parameters.AddWithValue("@ruc", (object)(s.Ruc ?? ""));
                    cmd.Parameters.AddWithValue("@razon_social", (object)(s.RazonSocial ?? ""));

                    cmd.Parameters.AddWithValue("@email", (object)(s.Email ?? ""));
                    cmd.Parameters.AddWithValue("@telefono", (object)(s.Telefono ?? ""));
                    cmd.Parameters.AddWithValue("@direccion", (object)(s.Direccion ?? ""));
                    cmd.Parameters.AddWithValue("@ciudad", (object)(s.Ciudad ?? ""));
                    cmd.Parameters.AddWithValue("@provincia", (object)(s.Provincia ?? ""));
                    cmd.Parameters.AddWithValue("@pais", (object)(s.Pais ?? ""));

                    cmd.Parameters.AddWithValue("@representante_legal", (object)(s.RepresentanteLegal ?? ""));
                    cmd.Parameters.AddWithValue("@cedula_representante", (object)(s.CedulaRepresentante ?? ""));

                    cmd.Parameters.AddWithValue("@tipo_operacion", (object)(s.TipoOperacion ?? ""));
                    cmd.Parameters.AddWithValue("@descripcion_operacion", (object)(s.DescripcionOperacion ?? ""));
                    cmd.Parameters.AddWithValue("@observaciones", (object)(s.Observaciones ?? ""));

                    cmd.Parameters.AddWithValue("@codigo_tecnico", (object)s.CodigoTecnico ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@updated_by", (object)(s.UpdatedBy ?? "sistema"));

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ✅ COMPATIBILIDAD
        public bool Actualizar(SolicitudAOCR s) => ActualizarGeneral(s);

        public bool CambiarEstado(int id, string estado, int usuario, string obs = "")
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                const string sql = @"
UPDATE aocr_tbsolicitud
SET estado=@e,
    observaciones=@o,
    updated_at=NOW(),
    updated_by=@u
WHERE codigo_solicitud=@id AND deleted_at IS NULL;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    var estadoNormalizado = EstadoSolicitud.Normalizar(estado);
                    cmd.Parameters.AddWithValue("@e", estadoNormalizado);
                    cmd.Parameters.AddWithValue("@o", obs ?? "");
                    cmd.Parameters.AddWithValue("@u", usuario.ToString());
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ============================
        // ASIGNAR INSPECTORES (CORREGIDO A ESQUEMA REAL)
        // ✅ aocr_tbinspeccion tiene codigo_inspector, fecha_programada, hora_programada, comentarios/observaciones
        // ============================
        public bool AsignarInspectores(int codigoSolicitud, int inspectorPrincipal, int? inspectorApoyo,
            DateTime fecha, string obs, out string mensaje)
        {
            try
            {
                using (var cn = new NpgsqlConnection(ConnectionString))
                {
                    cn.Open();

                    // 1) Crear/actualizar una inspección "programada" para la solicitud
                    // (si tú ya manejas varias inspecciones, aquí se puede ajustar)
                    const string sql = @"
INSERT INTO aocr_tbinspeccion (codigo_solicitud, tipo, fecha_programada, codigo_inspector, comentarios, estado, created_at, created_by)
VALUES (@sol, 1, @fecha, @insp, @obs, 'PROGRAMADA', NOW(), @usr)
RETURNING codigo_inspeccion;";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@sol", codigoSolicitud);
                        cmd.Parameters.AddWithValue("@fecha", fecha.Date);
                        cmd.Parameters.AddWithValue("@insp", inspectorPrincipal);
                        cmd.Parameters.AddWithValue("@obs", obs ?? "");
                        cmd.Parameters.AddWithValue("@usr", "sistema");

                        var idInspeccion = Convert.ToInt32(cmd.ExecuteScalar());

                        // 2) Cambiar estado de la solicitud
                        CambiarEstado(codigoSolicitud, "INSPECCION_ASIGNADA", inspectorPrincipal, "Inspección programada. Inspección #" + idInspeccion);

                        mensaje = "Asignación realizada con éxito. Inspección creada: " + idInspeccion;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                mensaje = "Error en base de datos: " + ex.Message;
                return false;
            }
        }

        // ============================
        // ACTUALIZAR TÉCNICO (OK)
        // ============================
        public bool ActualizarTecnico(int solicitudId, int tecnicoId, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                const string sql = @"
UPDATE aocr_tbsolicitud
SET codigo_tecnico=@t,
    updated_at=NOW(),
    updated_by=@u
WHERE codigo_solicitud=@id AND deleted_at IS NULL;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@t", tecnicoId);
                    cmd.Parameters.AddWithValue("@u", usuarioId.ToString());
                    cmd.Parameters.AddWithValue("@id", solicitudId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ============================
        // INTERNOS
        // ============================
        private List<SolicitudAOCR> ObtenerPorFiltro(string where, Action<NpgsqlCommand> parametros = null)
        {
            var lista = new List<SolicitudAOCR>();
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                string sql = $@"SELECT * FROM aocr_tbsolicitud WHERE {where} ORDER BY fecha_solicitud DESC";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    parametros?.Invoke(cmd);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read()) lista.Add(Mapear(rd));
                    }
                }
            }
            return lista;
        }

        private SolicitudAOCR Mapear(IDataRecord rd)
        {
            return new SolicitudAOCR
            {
                CodigoSolicitud = GetInt(rd, "codigo_solicitud"),
                NumeroSolicitud = GetString(rd, "numero_solicitud"),
                FechaSolicitud = GetNullableDateTime(rd, "fecha_solicitud"),
                TipoSolicitud = GetNullableInt(rd, "tipo_solicitud"),
                Estado = GetString(rd, "estado"),

                NombreOperador = GetString(rd, "nombre_operador"),
                Ruc = GetString(rd, "ruc"),
                RazonSocial = GetString(rd, "razon_social"),

                Email = GetString(rd, "email"),
                Telefono = GetString(rd, "telefono"),
                Direccion = GetString(rd, "direccion"),
                Ciudad = GetString(rd, "ciudad"),
                Provincia = GetString(rd, "provincia"),
                Pais = GetString(rd, "pais"),

                RepresentanteLegal = GetString(rd, "representante_legal"),
                CedulaRepresentante = GetString(rd, "cedula_representante"),

                TipoOperacion = GetString(rd, "tipo_operacion"),
                DescripcionOperacion = GetString(rd, "descripcion_operacion"),
                Observaciones = GetString(rd, "observaciones"),

                CodigoUsuario = GetInt(rd, "codigo_usuario"),
                CodigoTecnico = GetNullableInt(rd, "codigo_tecnico"),

                CreatedAt = GetNullableDateTime(rd, "created_at"),
                UpdatedAt = GetNullableDateTime(rd, "updated_at"),
                CreatedBy = GetString(rd, "created_by"),
                UpdatedBy = GetString(rd, "updated_by"),

                DeletedAt = GetNullableDateTime(rd, "deleted_at"),
                DeletedBy = GetString(rd, "deleted_by")
            };
        }

        private static string GetString(IDataRecord rd, string col)
            => rd[col] == DBNull.Value ? null : rd[col].ToString();

        private static int GetInt(IDataRecord rd, string col)
            => rd[col] == DBNull.Value ? 0 : Convert.ToInt32(rd[col]);

        private static int? GetNullableInt(IDataRecord rd, string col)
            => rd[col] == DBNull.Value ? (int?)null : Convert.ToInt32(rd[col]);

        private static DateTime? GetNullableDateTime(IDataRecord rd, string col)
            => rd[col] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd[col]);
    }
}
