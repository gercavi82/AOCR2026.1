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
                    id AS Id,
                    usuario_rt_id AS UsuarioRtId,
                    compania_id AS CompaniaId,
                    estado AS Estado,
                    declaracion_aceptada AS DeclaracionAceptada,
                    declaracion_texto AS DeclaracionTexto,
                    fecha_envio AS FechaEnvio,
                    observacion_coordinador AS ObservacionCoordinador,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM aocr_solicitud_rt
                WHERE usuario_rt_id = @usuarioId
                ORDER BY id DESC
                LIMIT 1;";

            using (var cn = CrearConexion())
            {
                return cn.QueryFirstOrDefault<SolicitudRTModel>(sql, new { usuarioId });
            }
        }

        public SolicitudRTModel GetSolicitudById(int solicitudId)
        {
            const string sql = @"
                SELECT
                    id AS Id,
                    usuario_rt_id AS UsuarioRtId,
                    compania_id AS CompaniaId,
                    estado AS Estado,
                    declaracion_aceptada AS DeclaracionAceptada,
                    declaracion_texto AS DeclaracionTexto,
                    fecha_envio AS FechaEnvio,
                    observacion_coordinador AS ObservacionCoordinador,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM aocr_solicitud_rt
                WHERE id = @id;";

            using (var cn = CrearConexion())
            {
                return cn.QueryFirstOrDefault<SolicitudRTModel>(sql, new { id = solicitudId });
            }
        }

        public CompaniaModel GetCompaniaById(int companiaId)
        {
            const string sql = @"
                SELECT
                    id AS Id,
                    razon_social AS RazonSocial,
                    ruc AS Ruc,
                    telefono AS Telefono,
                    email_contacto AS EmailContacto,
                    area_contable_json::text AS AreaContableJson,
                    created_at AS CreatedAt
                FROM aocr_compania
                WHERE id = @id;";

            using (var cn = CrearConexion())
            {
                return cn.QueryFirstOrDefault<CompaniaModel>(sql, new { id = companiaId });
            }
        }

        public bool ExisteRuc(string ruc, int? companiaId)
        {
            const string sql = @"SELECT id FROM aocr_compania WHERE LOWER(ruc) = LOWER(@ruc) LIMIT 1;";
            using (var cn = CrearConexion())
            {
                var id = cn.ExecuteScalar<int?>(sql, new { ruc });
                return id.HasValue && (!companiaId.HasValue || id.Value != companiaId.Value);
            }
        }

        public bool ExisteEmail(string email, int? companiaId)
        {
            const string sql = @"SELECT id FROM aocr_compania WHERE LOWER(email_contacto) = LOWER(@email) LIMIT 1;";
            using (var cn = CrearConexion())
            {
                var id = cn.ExecuteScalar<int?>(sql, new { email });
                return id.HasValue && (!companiaId.HasValue || id.Value != companiaId.Value);
            }
        }

        public int CreateCompania(CompaniaModel c)
        {
            const string sql = @"
                INSERT INTO aocr_compania
                    (razon_social, ruc, telefono, email_contacto, area_contable_json, created_at)
                VALUES
                    (@RazonSocial, @Ruc, @Telefono, @EmailContacto, CAST(@AreaContableJson AS jsonb), NOW())
                RETURNING id;";

            using (var cn = CrearConexion())
            {
                return cn.ExecuteScalar<int>(sql, c);
            }
        }

        public int CreateCompaniaYSolicitudBorrador(int usuarioId, CompaniaModel c, string textoDeclaracion)
        {
            const string sqlCompania = @"
                INSERT INTO aocr_compania
                    (razon_social, ruc, telefono, email_contacto, area_contable_json, created_at)
                VALUES
                    (@RazonSocial, @Ruc, @Telefono, @EmailContacto, CAST(@AreaContableJson AS jsonb), NOW())
                RETURNING id;";

            const string sqlSolicitud = @"
                INSERT INTO aocr_solicitud_rt
                    (usuario_rt_id, compania_id, estado, declaracion_aceptada, declaracion_texto, created_at, updated_at)
                VALUES
                    (@usuarioId, @companiaId, 'BORRADOR', FALSE, @texto, NOW(), NOW())
                RETURNING id;";

            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    var companiaId = cn.ExecuteScalar<int>(sqlCompania, c, tx);
                    var solicitudId = cn.ExecuteScalar<int>(sqlSolicitud, new { usuarioId, companiaId, texto = textoDeclaracion }, tx);
                    tx.Commit();
                    return solicitudId;
                }
            }
        }

        public void UpdateCompania(int companiaId, CompaniaModel c)
        {
            const string sql = @"
                UPDATE aocr_compania
                SET razon_social = @RazonSocial,
                    ruc = @Ruc,
                    telefono = @Telefono,
                    email_contacto = @EmailContacto,
                    area_contable_json = CAST(@AreaContableJson AS jsonb)
                WHERE id = @Id;";

            using (var cn = CrearConexion())
            {
                c.Id = companiaId;
                cn.Execute(sql, c);
            }
        }

        public int CreateSolicitudBorrador(int usuarioId, int companiaId, string textoDeclaracion)
        {
            const string sql = @"
                INSERT INTO aocr_solicitud_rt
                    (usuario_rt_id, compania_id, estado, declaracion_aceptada, declaracion_texto, created_at, updated_at)
                VALUES
                    (@usuarioId, @companiaId, 'BORRADOR', FALSE, @texto, NOW(), NOW())
                RETURNING id;";

            using (var cn = CrearConexion())
            {
                return cn.ExecuteScalar<int>(sql, new { usuarioId, companiaId, texto = textoDeclaracion });
            }
        }

        public void UpdateDeclaracionAceptada(int solicitudId, bool aceptada)
        {
            const string sql = @"
                UPDATE aocr_solicitud_rt
                SET declaracion_aceptada = @aceptada,
                    updated_at = NOW()
                WHERE id = @id;";

            using (var cn = CrearConexion())
            {
                cn.Execute(sql, new { id = solicitudId, aceptada });
            }
        }

        public void UpdateEstadoEnviada(int solicitudId, DateTime fechaEnvio)
        {
            const string sql = @"
                UPDATE aocr_solicitud_rt
                SET estado = 'ENVIADA',
                    fecha_envio = @fechaEnvio,
                    updated_at = NOW()
                WHERE id = @id;";

            using (var cn = CrearConexion())
            {
                cn.Execute(sql, new { id = solicitudId, fechaEnvio });
            }
        }

        public void InsertHistorialEstado(int solicitudId, string estado, int usuarioId, string motivo)
        {
            const string sql = @"
                INSERT INTO aocr_solicitud_rt_historial
                    (solicitud_rt_id, estado, motivo, usuario_id, created_at)
                VALUES
                    (@solicitudId, @estado, @motivo, @usuarioId, NOW());";

            using (var cn = CrearConexion())
            {
                cn.Execute(sql, new { solicitudId, estado, motivo, usuarioId });
            }
        }
    }
}
