using System;
using System.Configuration;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class CertificadoDAO
    {
        private NpgsqlConnection CrearConexion()
        {
            var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
                throw new Exception("No existe la cadena de conexión 'AOCRConnection' en el config.");
            return new NpgsqlConnection(cs);
        }

        public int Crear(Certificado cert)
        {
            const string sql = @"
                INSERT INTO aocr_tbcertificado
                (codigo_solicitud, numero_certificado, tipo, estado, fecha_emision, fecha_vencimiento,
                 ruta_documento, observaciones, emitido_por, aprobado_por, created_at, created_by, updated_at, updated_by)
                VALUES
                (@codSol, @num, @tipo, @estado, @fe, @fv,
                 @ruta, @obs, @emit, @aprob, @cat, @cby, @uat, @uby)
                RETURNING codigo_certificado;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codSol", cert.CodigoSolicitud);
                cmd.Parameters.AddWithValue("@num", (object)cert.NumeroCertificado ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tipo", (object)cert.Tipo ?? "AOCR");
                cmd.Parameters.AddWithValue("@estado", (object)cert.Estado ?? "GENERADO");
                cmd.Parameters.AddWithValue("@fe", (object)cert.FechaEmision ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@fv", (object)cert.FechaVencimiento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruta", (object)cert.RutaDocumento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@obs", (object)cert.Observaciones ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@emit", (object)cert.EmitidoPor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@aprob", (object)cert.AprobadoPor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cat", (object)cert.CreatedAt ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@cby", cert.CreatedBy);
                cmd.Parameters.AddWithValue("@uat", (object)cert.UpdatedAt ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@uby", cert.UpdatedBy);

                cn.Open();
                var id = cmd.ExecuteScalar();
                return (id == null || id == DBNull.Value) ? 0 : Convert.ToInt32(id);
            }
        }

        public bool Actualizar(Certificado cert)
        {
            const string sql = @"
                UPDATE aocr_tbcertificado
                SET estado=@estado, ruta_documento=@ruta, observaciones=@obs,
                    emitido_por=@emit, aprobado_por=@aprob, fecha_emision=@fe, fecha_vencimiento=@fv,
                    updated_at=@uat, updated_by=@uby
                WHERE codigo_certificado=@id;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", cert.CodigoCertificado);
                cmd.Parameters.AddWithValue("@estado", (object)cert.Estado ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruta", (object)cert.RutaDocumento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@obs", (object)cert.Observaciones ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@emit", (object)cert.EmitidoPor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@aprob", (object)cert.AprobadoPor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fe", (object)cert.FechaEmision ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fv", (object)cert.FechaVencimiento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uat", DateTime.Now);
                cmd.Parameters.AddWithValue("@uby", cert.UpdatedBy);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public Certificado ObtenerPorId(int id)
        {
            const string sql = @"
                SELECT codigo_certificado AS codigocertificado,
                       codigo_solicitud AS codigosolicitud,
                       numero_certificado AS numerocertificado,
                       tipo,
                       estado,
                       fecha_emision AS fechaemision,
                       fecha_vencimiento AS fechavencimiento,
                       ruta_documento AS rutadocumento,
                       observaciones,
                       emitido_por AS emitidopor,
                       aprobado_por AS aprobadopor,
                       created_at AS createdat,
                       created_by AS createdby,
                       updated_at AS updatedat,
                       updated_by AS updatedby
                FROM aocr_tbcertificado
                WHERE codigo_certificado=@id;";
            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;

                    return new Certificado
                    {
                        CodigoCertificado = rd["codigocertificado"] != DBNull.Value ? Convert.ToInt32(rd["codigocertificado"]) : 0,
                        CodigoSolicitud = rd["codigosolicitud"] != DBNull.Value ? Convert.ToInt32(rd["codigosolicitud"]) : 0,
                        NumeroCertificado = rd["numerocertificado"]?.ToString(),
                        Tipo = rd["tipo"]?.ToString(),
                        Estado = rd["estado"]?.ToString(),
                        FechaEmision = rd["fechaemision"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["fechaemision"]) : null,
                        FechaVencimiento = rd["fechavencimiento"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["fechavencimiento"]) : null,
                        RutaDocumento = rd["rutadocumento"]?.ToString(),
                        Observaciones = rd["observaciones"]?.ToString(),
                        EmitidoPor = rd["emitidopor"]?.ToString(),
                        AprobadoPor = rd["aprobadopor"]?.ToString(),
                        CreatedAt = rd["createdat"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["createdat"]) : null,
                        CreatedBy = rd["createdby"] != DBNull.Value ? Convert.ToInt32(rd["createdby"]) : 0,
                        UpdatedAt = rd["updatedat"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["updatedat"]) : null,
                        UpdatedBy = rd["updatedby"] != DBNull.Value ? Convert.ToInt32(rd["updatedby"]) : 0
                    };
                }
            }
        }

        public Certificado ObtenerPorSolicitud(int solicitudId)
        {
            const string sql = @"
                SELECT codigo_certificado AS codigocertificado,
                       codigo_solicitud AS codigosolicitud,
                       numero_certificado AS numerocertificado,
                       tipo,
                       estado,
                       fecha_emision AS fechaemision,
                       fecha_vencimiento AS fechavencimiento,
                       ruta_documento AS rutadocumento,
                       observaciones,
                       emitido_por AS emitidopor,
                       aprobado_por AS aprobadopor,
                       created_at AS createdat,
                       created_by AS createdby,
                       updated_at AS updatedat,
                       updated_by AS updatedby
                FROM aocr_tbcertificado
                WHERE codigo_solicitud=@id
                ORDER BY codigo_certificado DESC
                LIMIT 1;";
            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", solicitudId);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;

                    return new Certificado
                    {
                        CodigoCertificado = rd["codigocertificado"] != DBNull.Value ? Convert.ToInt32(rd["codigocertificado"]) : 0,
                        CodigoSolicitud = rd["codigosolicitud"] != DBNull.Value ? Convert.ToInt32(rd["codigosolicitud"]) : 0,
                        NumeroCertificado = rd["numerocertificado"]?.ToString(),
                        Tipo = rd["tipo"]?.ToString(),
                        Estado = rd["estado"]?.ToString(),
                        FechaEmision = rd["fechaemision"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["fechaemision"]) : null,
                        FechaVencimiento = rd["fechavencimiento"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["fechavencimiento"]) : null,
                        RutaDocumento = rd["rutadocumento"]?.ToString(),
                        Observaciones = rd["observaciones"]?.ToString(),
                        EmitidoPor = rd["emitidopor"]?.ToString(),
                        AprobadoPor = rd["aprobadopor"]?.ToString(),
                        CreatedAt = rd["createdat"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["createdat"]) : null,
                        CreatedBy = rd["createdby"] != DBNull.Value ? Convert.ToInt32(rd["createdby"]) : 0,
                        UpdatedAt = rd["updatedat"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["updatedat"]) : null,
                        UpdatedBy = rd["updatedby"] != DBNull.Value ? Convert.ToInt32(rd["updatedby"]) : 0
                    };
                }
            }
        }
    }
}
