using System;
using System.Configuration;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class RevisionDocumentalDAO
    {
        private string ConnectionString =>
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        public bool RegistrarRevision(
            int codigoSolicitud,
            int codigoDocumento,
            string decision,
            string observacion,
            int codigoUsuarioRevisor,
            string usuarioRegistro)
        {
            if (codigoSolicitud <= 0 || codigoDocumento <= 0 || string.IsNullOrWhiteSpace(decision))
            {
                return false;
            }

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                if (!ExisteTabla(cn, "aocr_tbrevision_documental"))
                {
                    return false;
                }

                const string sql = @"
                    INSERT INTO aocr_tbrevision_documental
                    (
                        codigo_solicitud,
                        codigo_documento,
                        decision,
                        observacion,
                        codigo_usuario_revisor,
                        fecha_revision,
                        created_at,
                        created_by
                    )
                    VALUES
                    (
                        @codigo_solicitud,
                        @codigo_documento,
                        @decision,
                        @observacion,
                        @codigo_usuario_revisor,
                        NOW(),
                        NOW(),
                        @created_by
                    );";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@codigo_documento", codigoDocumento);
                    cmd.Parameters.AddWithValue("@decision", decision.Trim().ToUpperInvariant());
                    cmd.Parameters.AddWithValue("@observacion", (object)(observacion ?? string.Empty));
                    cmd.Parameters.AddWithValue("@codigo_usuario_revisor", codigoUsuarioRevisor);
                    cmd.Parameters.AddWithValue("@created_by", (object)(usuarioRegistro ?? "sistema"));
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool RegistrarEventoHistorial(
            int codigoSolicitud,
            int? codigoDocumento,
            string evento,
            string detalle,
            int? codigoUsuario,
            string usuarioRegistro)
        {
            if (codigoSolicitud <= 0 || string.IsNullOrWhiteSpace(evento))
            {
                return false;
            }

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                if (!ExisteTabla(cn, "aocr_tbhistorial_documental"))
                {
                    return false;
                }

                const string sql = @"
                    INSERT INTO aocr_tbhistorial_documental
                    (
                        codigo_solicitud,
                        codigo_documento,
                        evento,
                        detalle,
                        codigo_usuario,
                        fecha_evento,
                        created_at,
                        created_by
                    )
                    VALUES
                    (
                        @codigo_solicitud,
                        @codigo_documento,
                        @evento,
                        @detalle,
                        @codigo_usuario,
                        NOW(),
                        NOW(),
                        @created_by
                    );";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    cmd.Parameters.AddWithValue("@codigo_documento", (object)codigoDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@evento", evento.Trim().ToUpperInvariant());
                    cmd.Parameters.AddWithValue("@detalle", (object)(detalle ?? string.Empty));
                    cmd.Parameters.AddWithValue("@codigo_usuario", (object)codigoUsuario ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@created_by", (object)(usuarioRegistro ?? "sistema"));
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        private static bool ExisteTabla(NpgsqlConnection cn, string tabla)
        {
            const string sql = "SELECT to_regclass(@tabla) IS NOT NULL;";
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value && Convert.ToBoolean(result);
            }
        }
    }
}
