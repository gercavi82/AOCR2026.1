using System;
using Dapper;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class DeclaracionTemporalDAO
    {
        private NpgsqlConnection CrearConexion() => ConexionDAO.CrearConexion();

        public DeclaracionTemporal GetByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            const string sql = @"
                SELECT
                    email AS Email,
                    identificacion AS Identificacion,
                    empresa_codigo AS EmpresaCodigo,
                    empresa_nombre AS EmpresaNombre,
                    nombres AS Nombres,
                    apellidos AS Apellidos,
                    aceptada AS Aceptada,
                    ip AS Ip,
                    user_agent AS UserAgent,
                    expires_at AS ExpiresAt,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM aocr_declaracion_tmp
                WHERE LOWER(email) = LOWER(@email)
                  AND expires_at > NOW()
                ORDER BY updated_at DESC
                LIMIT 1;";

            using (var cn = CrearConexion())
            {
                return cn.QueryFirstOrDefault<DeclaracionTemporal>(sql, new { email });
            }
        }

        public void Upsert(DeclaracionTemporal model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Email))
                return;

            const string sql = @"
                INSERT INTO aocr_declaracion_tmp
                    (email, identificacion, empresa_codigo, empresa_nombre, nombres, apellidos, aceptada, ip, user_agent, expires_at, created_at, updated_at)
                VALUES
                    (@Email, @Identificacion, @EmpresaCodigo, @EmpresaNombre, @Nombres, @Apellidos, @Aceptada, @Ip, @UserAgent, (NOW() + INTERVAL '15 minutes'), NOW(), NOW())
                ON CONFLICT (email) DO UPDATE
                SET identificacion = EXCLUDED.identificacion,
                    empresa_codigo = EXCLUDED.empresa_codigo,
                    empresa_nombre = EXCLUDED.empresa_nombre,
                    nombres = EXCLUDED.nombres,
                    apellidos = EXCLUDED.apellidos,
                    aceptada = EXCLUDED.aceptada,
                    ip = EXCLUDED.ip,
                    user_agent = EXCLUDED.user_agent,
                    expires_at = (NOW() + INTERVAL '15 minutes'),
                    updated_at = NOW();";

            using (var cn = CrearConexion())
            {
                cn.Execute(sql, new
                {
                    Email = (model.Email ?? string.Empty).Trim().ToLower(),
                    Identificacion = (model.Identificacion ?? string.Empty).Trim(),
                    EmpresaCodigo = (model.EmpresaCodigo ?? string.Empty).Trim(),
                    EmpresaNombre = (model.EmpresaNombre ?? string.Empty).Trim(),
                    Nombres = (model.Nombres ?? string.Empty).Trim().ToUpper(),
                    Apellidos = (model.Apellidos ?? string.Empty).Trim().ToUpper(),
                    Aceptada = model.Aceptada,
                    Ip = (model.Ip ?? string.Empty).Trim(),
                    UserAgent = (model.UserAgent ?? string.Empty).Trim()
                });
            }
        }

        public void DeleteByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            const string sql = @"DELETE FROM aocr_declaracion_tmp WHERE LOWER(email) = LOWER(@email);";
            using (var cn = CrearConexion())
            {
                cn.Execute(sql, new { email });
            }
        }

        public void MoveToHistorialByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            const string sqlInsert = @"
                WITH src AS (
                    SELECT *
                    FROM aocr_declaracion_tmp
                    WHERE LOWER(email) = LOWER(@email)
                    ORDER BY updated_at DESC
                    LIMIT 1
                )
                INSERT INTO aocr_declaracion_historial
                    (email, identificacion, empresa_codigo, empresa_nombre, nombres, apellidos,
                     aceptada, ip, user_agent, created_at, updated_at, finalized_at)
                SELECT
                    email, identificacion, empresa_codigo, empresa_nombre, nombres, apellidos,
                    aceptada, ip, user_agent, created_at, updated_at, NOW()
                FROM src;";

            const string sqlDelete = @"DELETE FROM aocr_declaracion_tmp WHERE LOWER(email) = LOWER(@email);";

            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    cn.Execute(sqlInsert, new { email }, tx);
                    cn.Execute(sqlDelete, new { email }, tx);
                    tx.Commit();
                }
            }
        }
    }

    public class DeclaracionTemporal
    {
        public string Email { get; set; }
        public string Identificacion { get; set; }
        public string EmpresaCodigo { get; set; }
        public string EmpresaNombre { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public bool Aceptada { get; set; }
        public string Ip { get; set; }
        public string UserAgent { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
