using System;
using System.Collections.Generic;
using System.Configuration;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class RevisionDocumentalDAO
    {
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;

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
                EnsureSchema(cn);

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

        public Dictionary<int, Tuple<string, string>> ObtenerUltimasRevisionesPorSolicitud(int codigoSolicitud)
        {
            var resultado = new Dictionary<int, Tuple<string, string>>();
            if (codigoSolicitud <= 0)
            {
                return resultado;
            }

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    SELECT codigo_documento, decision, observacion
                    FROM
                    (
                        SELECT
                            codigo_documento,
                            decision,
                            observacion,
                            ROW_NUMBER() OVER
                            (
                                PARTITION BY codigo_documento
                                ORDER BY COALESCE(fecha_revision, created_at) DESC, created_at DESC, codigo_documento DESC
                            ) AS rn
                        FROM aocr_tbrevision_documental
                        WHERE codigo_solicitud = @codigo_solicitud
                    ) q
                    WHERE rn = 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var codigoDocumento = rd["codigo_documento"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_documento"]);
                            if (codigoDocumento <= 0)
                            {
                                continue;
                            }

                            var decision = rd["decision"] == DBNull.Value ? string.Empty : rd["decision"].ToString();
                            var observacion = rd["observacion"] == DBNull.Value ? string.Empty : rd["observacion"].ToString();
                            resultado[codigoDocumento] = Tuple.Create(
                                (decision ?? string.Empty).Trim().ToUpperInvariant(),
                                (observacion ?? string.Empty).Trim());
                        }
                    }
                }
            }

            return resultado;
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
                EnsureSchema(cn);

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
                    CREATE TABLE IF NOT EXISTS public.aocr_tbrevision_documental
                    (
                        id SERIAL PRIMARY KEY,
                        codigo_solicitud INTEGER NOT NULL,
                        codigo_documento INTEGER NOT NULL,
                        decision VARCHAR(60) NOT NULL,
                        observacion TEXT,
                        codigo_usuario_revisor INTEGER,
                        fecha_revision TIMESTAMP NOT NULL DEFAULT NOW(),
                        created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        created_by VARCHAR(150)
                    );

                    CREATE INDEX IF NOT EXISTS idx_revision_documental_solicitud_doc
                        ON public.aocr_tbrevision_documental(codigo_solicitud, codigo_documento, fecha_revision DESC, created_at DESC);

                    CREATE TABLE IF NOT EXISTS public.aocr_tbhistorial_documental
                    (
                        id SERIAL PRIMARY KEY,
                        codigo_solicitud INTEGER NOT NULL,
                        codigo_documento INTEGER,
                        evento VARCHAR(120) NOT NULL,
                        detalle TEXT,
                        codigo_usuario INTEGER,
                        fecha_evento TIMESTAMP NOT NULL DEFAULT NOW(),
                        created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        created_by VARCHAR(150)
                    );

                    CREATE INDEX IF NOT EXISTS idx_historial_documental_solicitud
                        ON public.aocr_tbhistorial_documental(codigo_solicitud, fecha_evento DESC, created_at DESC);";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }
    }
}
