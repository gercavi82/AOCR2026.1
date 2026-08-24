using System;
using System.Data;
using Dapper;
using Npgsql;
using CapaModelo.RT;

namespace CapaDatos.DAOs
{
    public class RTDao
    {
        private NpgsqlConnection CrearConexion() => ConexionDAO.CrearConexion();

        public SolicitudRTModel GetSolicitudByUsuario(int usuarioId)
        {
            const string sql = @"
                SELECT
                    id::integer AS Id,
                    usuario_rt_id AS UsuarioRtId,
                    0 AS CompaniaId,
                    estado AS Estado,
                    declaracion_aceptada AS DeclaracionAceptada,
                    COALESCE(observacion_actual, '') AS DeclaracionTexto,
                    creado_en AS FechaEnvio,
                    observacion_actual AS ObservacionCoordinador,
                    creado_en AS CreatedAt,
                    actualizado_en AS UpdatedAt
                FROM django_aocr_registro_rt
                WHERE usuario_rt_id = @usuarioId
                ORDER BY id DESC
                LIMIT 1;";

            using (var cn = CrearConexion())
            {
                try
                {
                    return cn.QueryFirstOrDefault<SolicitudRTModel>(sql, new { usuarioId });
                }
                catch (PostgresException ex) when (EsTablaNoDisponible(ex))
                {
                    return null;
                }
            }
        }

        public SolicitudRTModel GetSolicitudById(int solicitudId)
        {
            const string sql = @"
                SELECT
                    id::integer AS Id,
                    usuario_rt_id AS UsuarioRtId,
                    0 AS CompaniaId,
                    estado AS Estado,
                    declaracion_aceptada AS DeclaracionAceptada,
                    COALESCE(observacion_actual, '') AS DeclaracionTexto,
                    creado_en AS FechaEnvio,
                    observacion_actual AS ObservacionCoordinador,
                    creado_en AS CreatedAt,
                    actualizado_en AS UpdatedAt
                FROM django_aocr_registro_rt
                WHERE id = @id;";

            using (var cn = CrearConexion())
            {
                try
                {
                    return cn.QueryFirstOrDefault<SolicitudRTModel>(sql, new { id = solicitudId });
                }
                catch (PostgresException ex) when (EsTablaNoDisponible(ex))
                {
                    return null;
                }
            }
        }

        public CompaniaModel GetCompaniaById(int companiaId)
        {
            return null;
        }

        public bool ExisteRuc(string ruc, int? companiaId)
        {
            const string sql = @"SELECT id FROM django_aocr_registro_rt WHERE LOWER(identificacion) = LOWER(@ruc) LIMIT 1;";
            using (var cn = CrearConexion())
            {
                var id = cn.ExecuteScalar<int?>(sql, new { ruc });
                return id.HasValue && (!companiaId.HasValue || id.Value != companiaId.Value);
            }
        }

        public bool ExisteEmail(string email, int? companiaId)
        {
            const string sql = @"SELECT id FROM django_aocr_registro_rt WHERE LOWER(email) = LOWER(@email) LIMIT 1;";
            using (var cn = CrearConexion())
            {
                var id = cn.ExecuteScalar<int?>(sql, new { email });
                return id.HasValue && (!companiaId.HasValue || id.Value != companiaId.Value);
            }
        }

        public int CreateCompania(CompaniaModel c)
        {
            throw new NotSupportedException("El modelo real de RT no tiene una tabla independiente de compañías.");
        }

        public int CreateCompaniaYSolicitudBorrador(int usuarioId, CompaniaModel c, string textoDeclaracion)
        {
            const string sqlSolicitud = @"
                INSERT INTO django_aocr_registro_rt
                    (usuario_rt_id, compania, email, nombre, identificacion, estado,
                     declaracion_aceptada, observacion_actual, creado_en, actualizado_en)
                VALUES
                    (@usuarioId, @RazonSocial, @EmailContacto, @RazonSocial, @Ruc,
                     'BORRADOR', FALSE, @texto, NOW(), NOW())
                RETURNING id;";

            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    var solicitudId = cn.ExecuteScalar<long>(sqlSolicitud, new
                    {
                        usuarioId,
                        c.RazonSocial,
                        c.EmailContacto,
                        c.Ruc,
                        texto = textoDeclaracion
                    }, tx);
                    tx.Commit();
                    return checked((int)solicitudId);
                }
            }
        }

        public void UpdateCompania(int companiaId, CompaniaModel c)
        {
            throw new NotSupportedException("El modelo real de RT guarda los datos de compañía en el expediente.");
        }

        public int CreateSolicitudBorrador(int usuarioId, int companiaId, string textoDeclaracion)
        {
            const string sql = @"
                INSERT INTO django_aocr_registro_rt
                    (usuario_rt_id, compania, estado, declaracion_aceptada, observacion_actual, creado_en, actualizado_en)
                VALUES
                    (@usuarioId, '', 'BORRADOR', FALSE, @texto, NOW(), NOW())
                RETURNING id;";

            using (var cn = CrearConexion())
            {
                return cn.ExecuteScalar<int>(sql, new { usuarioId, companiaId, texto = textoDeclaracion });
            }
        }

        public void UpdateDeclaracionAceptada(int solicitudId, bool aceptada, string textoDeclaracion = null)
        {
            const string sql = @"
                UPDATE django_aocr_registro_rt
                SET declaracion_aceptada = @aceptada,
                    observacion_actual = COALESCE(NULLIF(@textoDeclaracion, ''), observacion_actual),
                    actualizado_en = NOW()
                WHERE id = @id;";

            using (var cn = CrearConexion())
            {
                cn.Execute(sql, new { id = solicitudId, aceptada, textoDeclaracion });
            }
        }

        public void UpdateEstadoEnviada(int solicitudId, DateTime fechaEnvio)
        {
            const string sql = @"
                UPDATE django_aocr_registro_rt
                SET estado = 'ENVIADA',
                    actualizado_en = @fechaEnvio
                WHERE id = @id;";

            using (var cn = CrearConexion())
            {
                cn.Execute(sql, new { id = solicitudId, fechaEnvio });
            }
        }

        public void UpdateEstado(int solicitudId, string estado, string observacionCoordinador = null, DateTime? fechaEnvio = null)
        {
            const string sql = @"
                UPDATE django_aocr_registro_rt
                SET estado = @estado,
                    observacion_actual = CASE
                        WHEN @actualizarObservacion THEN @observacionCoordinador
                        ELSE observacion_actual
                    END,
                    actualizado_en = COALESCE(@fechaEnvio, actualizado_en)
                WHERE id = @id;";

            using (var cn = CrearConexion())
            {
                cn.Execute(sql, new
                {
                    id = solicitudId,
                    estado,
                    observacionCoordinador,
                    fechaEnvio,
                    actualizarObservacion = observacionCoordinador != null
                });
            }
        }

        // Backfill para designaciones RT legacy que nunca generaron expediente en django_aocr_registro_rt.
        public int CrearRegistroLegacyEnRevision(int usuarioId, string compania, string email, string nombre, string identificacion)
        {
            const string sql = @"
                INSERT INTO django_aocr_registro_rt
                    (usuario_rt_id, compania, email, nombre, identificacion, estado,
                     declaracion_aceptada, observacion_actual, creado_en, actualizado_en)
                VALUES
                    (@usuarioId, @compania, @email, @nombre, @identificacion, 'EN_REVISION_COORDINADOR', TRUE,
                     'Expediente generado automaticamente para designacion RT legacy pendiente de asignacion de inspector.',
                     NOW(), NOW())
                RETURNING id;";

            using (var cn = CrearConexion())
            {
                return cn.ExecuteScalar<int>(sql, new
                {
                    usuarioId,
                    compania = compania ?? string.Empty,
                    email = email ?? string.Empty,
                    nombre = nombre ?? string.Empty,
                    identificacion = identificacion ?? string.Empty
                });
            }
        }

        public void InsertHistorialEstado(int solicitudId, string estado, int usuarioId, string motivo)
        {
            const string sql = @"
                INSERT INTO django_aocr_registro_rt_observacion
                    (rol_snapshot, observacion, tipo, estado_origen, estado_destino,
                     creado_en, registro_id, usuario_id)
                SELECT COALESCE(u.rol, 'SISTEMA'), @motivo, 'CAMBIO_ESTADO', r.estado,
                       @estado, NOW(), r.id, NULLIF(@usuarioId, 0)
                FROM django_aocr_registro_rt r
                LEFT JOIN usuario u ON u.idusuario = @usuarioId
                WHERE r.id = @solicitudId;";

            using (var cn = CrearConexion())
            {
                cn.Execute(sql, new { solicitudId, estado, motivo, usuarioId });
            }
        }

        private static bool EsTablaNoDisponible(PostgresException ex)
        {
            if (ex == null)
            {
                return false;
            }

            // 42P01: undefined_table, 42703: undefined_column.
            return ex.SqlState == "42P01"
                || ex.SqlState == "42703";
        }
    }
}
