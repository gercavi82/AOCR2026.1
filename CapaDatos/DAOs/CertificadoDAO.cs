using System;
using Dapper;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class CertificadoDAO
    {
        private NpgsqlConnection CrearConexion()
        {
            return ConexionDAO.CrearConexion();
        }

        public Certificado ObtenerPorId(int id)
        {
            using (var con = CrearConexion())
            {
                const string sql = @"
                    SELECT 
                        codigo_certificado   AS CodigoCertificado,
                        codigo_solicitud     AS CodigoSolicitud,
                        numero_certificado   AS NumeroCertificado,
                        fecha_emision        AS FechaEmision,
                        fecha_vencimiento    AS FechaVencimiento,
                        vigencia_anios       AS VigenciaAnios,
                        estado               AS Estado,
                        condiciones_especiales AS CondicionesEspeciales,
                        firmado_por          AS FirmadoPor,
                        ruta_pdf             AS RutaPdf,
                        codigo_verificacion  AS CodigoVerificacion
                    FROM aocr_tbcertificado
                    WHERE codigo_certificado = @id;";
                return con.QueryFirstOrDefault<Certificado>(sql, new { id });
            }
        }

        public Certificado ObtenerPorSolicitud(int codigoSolicitud)
        {
            using (var con = CrearConexion())
            {
                const string sql = @"
                    SELECT 
                        codigo_certificado   AS CodigoCertificado,
                        codigo_solicitud     AS CodigoSolicitud,
                        numero_certificado   AS NumeroCertificado,
                        fecha_emision        AS FechaEmision,
                        fecha_vencimiento    AS FechaVencimiento,
                        vigencia_anios       AS VigenciaAnios,
                        estado               AS Estado,
                        condiciones_especiales AS CondicionesEspeciales,
                        firmado_por          AS FirmadoPor,
                        ruta_pdf             AS RutaPdf,
                        codigo_verificacion  AS CodigoVerificacion
                    FROM aocr_tbcertificado
                    WHERE codigo_solicitud = @codigoSolicitud
                    ORDER BY codigo_certificado DESC
                    LIMIT 1;";
                return con.QueryFirstOrDefault<Certificado>(sql, new { codigoSolicitud });
            }
        }

        public int Crear(Certificado c)
        {
            if (string.IsNullOrWhiteSpace(c.FirmadoPor))
                throw new ArgumentException("El certificado debe ser firmado digitalmente antes de ser creado.");

            using (var con = CrearConexion())
            {
                const string sql = @"
                    INSERT INTO aocr_tbcertificado (
                        codigo_solicitud,
                        numero_certificado,
                        fecha_emision,
                        fecha_vencimiento,
                        vigencia_anios,
                        estado,
                        condiciones_especiales,
                        firmado_por,
                        ruta_pdf,
                        codigo_verificacion
                    ) VALUES (
                        @CodigoSolicitud,
                        @NumeroCertificado,
                        @FechaEmision,
                        @FechaVencimiento,
                        @VigenciaAnios,
                        @Estado,
                        @CondicionesEspeciales,
                        @FirmadoPor,
                        @RutaPdf,
                        @CodigoVerificacion
                    ) RETURNING codigo_certificado;";

                return con.ExecuteScalar<int>(sql, c);
            }
        }

        public bool Actualizar(Certificado c)
        {
            using (var con = CrearConexion())
            {
                const string sql = @"
                    UPDATE aocr_tbcertificado SET
                        numero_certificado = @NumeroCertificado,
                        fecha_emision = @FechaEmision,
                        fecha_vencimiento = @FechaVencimiento,
                        vigencia_anios = @VigenciaAnios,
                        estado = @Estado,
                        condiciones_especiales = @CondicionesEspeciales,
                        firmado_por = @FirmadoPor,
                        ruta_pdf = @RutaPdf,
                        codigo_verificacion = @CodigoVerificacion
                    WHERE codigo_certificado = @CodigoCertificado;";

                return con.Execute(sql, c) > 0;
            }
        }
    }
}
