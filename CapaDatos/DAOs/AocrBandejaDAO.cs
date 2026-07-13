using System.Collections.Generic;
using System.Configuration;
using System;
using System.Linq;
using CapaModelo.Common;
using Dapper;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class AocrBandejaDAO
    {
        private readonly string _connectionString;

        public AocrBandejaDAO()
        {
            var settings = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _connectionString = (settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString))
                ? settings.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public List<AocrBandejaDocumentoRow> ListarGeneradasFirmadas()
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                var columnasSolicitud = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
                var columnasInspeccion = ObtenerColumnasTabla(cn, "aocr_tbinspeccion");
                var columnasInforme = ObtenerColumnasTabla(cn, "aocr_tbinforme_inspeccion");
                var columnasCertificado = ObtenerColumnasTabla(cn, "aocr_tbcertificado");
                var columnasFirma = ObtenerColumnasTabla(cn, "aocr_tbfirma_documento");
                var columnasDocumento = ObtenerColumnasTabla(cn, "aocr_tbdocumento");
                var columnasUsuario = ObtenerColumnasTabla(cn, "usuario");
                var whereSolicitud = columnasSolicitud.Contains("deleted_at")
                    ? "WHERE s.deleted_at IS NULL"
                    : "WHERE 1 = 1";
                var joinUsuarioInspector = columnasInspeccion.Contains("codigo_inspector")
                    && columnasUsuario.Contains("idusuario")
                    ? "LEFT JOIN public.usuario ui ON ui.idusuario = i.codigo_inspector"
                    : string.Empty;
                var inspectorNombreUsuario = !string.IsNullOrWhiteSpace(joinUsuarioInspector)
                    ? "NULLIF(TRIM(COALESCE(ui.nombreusuario, '') || ' ' || COALESCE(ui.apellidousuario, '')), '')"
                    : "NULL::text";
                var inspectorCodigoUsuario = !string.IsNullOrWhiteSpace(joinUsuarioInspector) && columnasUsuario.Contains("codigousuario")
                    ? "NULLIF(TRIM(COALESCE(ui.codigousuario, '')), '')"
                    : "NULL::text";
                var estadosBandejaGeneradasFirmadas = new[]
                {
                    "GENERADO CONDICIONES LIMITACIONES",
                    "GENERADO CONDICIONES Y LIMITACIONES",
                    "EN REVISION COORDINADOR FINAL",
                    "EN REVISION COORDINADOR",
                    "APROBADO COORDINADOR",
                    "APROBADA COORDINADOR",
                    "ENVIADO DCAV",
                    "ENVIADO DIRDAC",
                    "ENVIADA DIRDAC",
                    "PENDIENTE FIRMA DIRDAC",
                    "FIRMADO DCAV",
                    "FIRMADA DIRDAC",
                    "FIRMADA",
                    "FINALIZADA",
                    "FINALIZADO",
                    "AOCR EN ELABORACION",
                    "AOCR GENERADA",
                    "GENERADA",
                    "AOCR EN REVISION",
                    "AOCR VALIDADO",
                    "VALIDADO",
                    "AOCR LEGALIZADO",
                    "LEGALIZADO",
                    "AOCR EMITIDO",
                    "AOCR EMITIDO RECIBIDO",
                    "AOCR ENTREGADO",
                    "ENVIADO A JEFATURA",
                    "ENVIADO COORDINADOR"
                };
                var estadosBandejaGeneradasFirmadasSql = FormatearListaSql(estadosBandejaGeneradasFirmadas);
                var estadoSolicitudNormalizadoSql = NormalizarEstadoSql("sb.EstadoSolicitudRaw");
                var estadoCertificadoNormalizadoSql = NormalizarEstadoSql("cert.EstadoCertificadoRaw");
                var estadoInformeNormalizadoSql = NormalizarEstadoSql("inf.EstadoInformeTecnicoRaw");

                var sql = $@"
                WITH solicitud_base AS (
                    SELECT
                        s.codigo_solicitud AS SolicitudId,
                        {ColumnaTexto("s", "numero_solicitud", columnasSolicitud)} AS NumeroSolicitud,
                        {ColumnaFecha("s", "fecha_solicitud", columnasSolicitud)} AS FechaSolicitud,
                        {ColumnaEntera("s", "tipo_solicitud", columnasSolicitud)} AS TipoSolicitud,
                        {ColumnaTexto("s", "estado", columnasSolicitud)} AS EstadoSolicitudRaw,
                        COALESCE(
                            NULLIF(TRIM(COALESCE({ColumnaTexto("s", "nombre_explotador", columnasSolicitud)}, '')), ''),
                            NULLIF(TRIM(COALESCE({ColumnaTexto("s", "razon_social", columnasSolicitud)}, '')), ''),
                            NULLIF(TRIM(COALESCE({ColumnaTexto("s", "razon_social_operador", columnasSolicitud)}, '')), ''),
                            NULLIF(TRIM(COALESCE({ColumnaTexto("s", "nombre_comercial", columnasSolicitud)}, '')), ''),
                            NULLIF(TRIM(COALESCE({ColumnaTexto("s", "nombre_operador", columnasSolicitud)}, '')), ''),
                            NULLIF(TRIM(COALESCE({ColumnaTexto("s", "nombre_compania", columnasSolicitud)}, '')), ''),
                            NULLIF(TRIM(COALESCE({ColumnaTexto("s", "compania_nombre", columnasSolicitud)}, '')), ''),
                            'SIN OPERADOR'
                        ) AS NombreExplotador,
                        NULLIF(TRIM(COALESCE({ColumnaTexto("s", "numero_aoc", columnasSolicitud)}, '')), '') AS NumeroAocBase,
                        COALESCE({ColumnaTexto("s", "companias_seleccionadas", columnasSolicitud)}, '') AS CompaniasSeleccionadas,
                        COALESCE({ColumnaEntera("s", "codigo_usuario", columnasSolicitud)}, 0) AS CodigoUsuario,
                        {ColumnaEntera("s", "codigo_tecnico", columnasSolicitud)} AS CodigoInspectorSolicitud,
                        NULLIF(TRIM(COALESCE({ColumnaTexto("s", "tecnico_responsable_nombre", columnasSolicitud)}, '')), '') AS InspectorNombreSolicitud,
                        NULLIF(TRIM(COALESCE({ColumnaTexto("s", "inspector_apoyo_nombre", columnasSolicitud)}, '')), '') AS InspectorApoyoNombreSolicitud
                    FROM public.aocr_tbsolicitud s
                    {whereSolicitud}
                )
                SELECT
                    sb.SolicitudId,
                    sb.NumeroSolicitud,
                    sb.FechaSolicitud,
                    sb.TipoSolicitud,
                    sb.EstadoSolicitudRaw,
                    sb.NombreExplotador,
                    sb.NumeroAocBase,
                    sb.CompaniasSeleccionadas,
                    sb.CodigoUsuario,
                    sb.CodigoInspectorSolicitud,
                    sb.InspectorNombreSolicitud,
                    sb.InspectorApoyoNombreSolicitud,
                    insp.InspeccionId,
                    insp.NumeroInspeccion,
                    insp.EstadoInspeccionRaw,
                    insp.ResultadoInspeccionRaw,
                    insp.CodigoInspectorInspeccion,
                    insp.InspectorPrincipalNombreInspeccion,
                    insp.FechaProgramadaInspeccion,
                    inf.InformeId,
                    inf.EstadoInformeTecnicoRaw,
                    inf.ResultadoTecnicoFinalRaw,
                    inf.InformeFirmadoInspector,
                    inf.InformeFirmadoDirdac,
                    inf.RutaInformePdf,
                    inf.RutaInformeFirmadoPdf,
                    inf.FechaFirmaInformeInspector,
                    inf.FechaFirmaInformeDireccion,
                    inf.FechaEnvioInformeDirdac,
                    cert.CertificadoId,
                    cert.NumeroAocrCertificado,
                    cert.EstadoCertificadoRaw,
                    cert.RutaCertificadoPdf,
                    cert.FechaEmisionCertificado,
                    cert.FechaActualizacionCertificado,
                    cert.EmitidoPor,
                    cert.AprobadoPor,
                    frec.FirmaReconocimientoId,
                    frec.NumeroAocrReconocimiento,
                    frec.RutaReconocimientoFirmado,
                    frec.NombreFirmanteReconocimiento,
                    frec.CargoFirmanteReconocimiento,
                    frec.FechaFirmaReconocimiento,
                    fcond.FirmaCondicionesId,
                    fcond.RutaCondicionesFirmado,
                    fcond.NombreFirmanteCondiciones,
                    fcond.CargoFirmanteCondiciones,
                    fcond.FechaFirmaCondiciones,
                    daocr.RutaAocrGenerada,
                    daocr.FechaAocrGenerada
                FROM solicitud_base sb
                LEFT JOIN LATERAL (
                    SELECT
                        {ColumnaEntera("i", "codigo_inspeccion", columnasInspeccion)} AS InspeccionId,
                        {ColumnaTexto("i", "numero_inspeccion", columnasInspeccion)} AS NumeroInspeccion,
                        {ColumnaTexto("i", "estado", columnasInspeccion)} AS EstadoInspeccionRaw,
                        {ColumnaTexto("i", "resultado", columnasInspeccion)} AS ResultadoInspeccionRaw,
                        {ColumnaEntera("i", "codigo_inspector", columnasInspeccion)} AS CodigoInspectorInspeccion,
                        COALESCE(
                            NULLIF(TRIM(COALESCE({ColumnaTexto("i", "inspector_principal_nombre", columnasInspeccion)}, '')), ''),
                            NULLIF(TRIM(COALESCE({ColumnaTexto("i", "inspector_principal", columnasInspeccion)}, '')), ''),
                            {inspectorNombreUsuario},
                            {inspectorCodigoUsuario}
                        ) AS InspectorPrincipalNombreInspeccion,
                        {ColumnaFecha("i", "fecha_programada", columnasInspeccion)} AS FechaProgramadaInspeccion
                    FROM public.aocr_tbinspeccion i
                    {joinUsuarioInspector}
                    WHERE i.codigo_solicitud = sb.SolicitudId
                    ORDER BY {ColumnaFecha("i", "fecha_programada", columnasInspeccion)} DESC NULLS LAST, {ColumnaEntera("i", "codigo_inspeccion", columnasInspeccion)} DESC
                    LIMIT 1
                ) insp ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        {ColumnaEntera("inf", "codigo_informe", columnasInforme)} AS InformeId,
                        {ColumnaTexto("inf", "estado_informe", columnasInforme)} AS EstadoInformeTecnicoRaw,
                        {ColumnaTexto("inf", "resultado", columnasInforme)} AS ResultadoTecnicoFinalRaw,
                        {ColumnaBoolean("inf", "firmado_inspector", columnasInforme)} AS InformeFirmadoInspector,
                        {ColumnaBoolean("inf", "firmado_dirdac", columnasInforme)} AS InformeFirmadoDirdac,
                        {ColumnaTexto("inf", "ruta_pdf", columnasInforme)} AS RutaInformePdf,
                        {ColumnaTexto("inf", "ruta_documento_firmado", columnasInforme)} AS RutaInformeFirmadoPdf,
                        {ColumnaFecha("inf", "fecha_firma_1", columnasInforme)} AS FechaFirmaInformeInspector,
                        {ColumnaFecha("inf", "fecha_firma_2", columnasInforme)} AS FechaFirmaInformeDireccion,
                        {ColumnaFecha("inf", "fecha_envio_dirdac", columnasInforme)} AS FechaEnvioInformeDirdac
                    FROM public.aocr_tbinforme_inspeccion inf
                    WHERE inf.codigo_inspeccion = insp.InspeccionId
                    ORDER BY {ColumnaEntera("inf", "version", columnasInforme)} DESC, {ColumnaEntera("inf", "codigo_informe", columnasInforme)} DESC
                    LIMIT 1
                ) inf ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        {ColumnaEntera("c", "codigo_certificado", columnasCertificado)} AS CertificadoId,
                        NULLIF(TRIM(COALESCE({ColumnaTexto("c", "numero_certificado", columnasCertificado)}, '')), '') AS NumeroAocrCertificado,
                        {ColumnaTexto("c", "estado", columnasCertificado)} AS EstadoCertificadoRaw,
                        COALESCE({ColumnaTexto("c", "ruta_documento", columnasCertificado)}, {ColumnaTexto("c", "ruta_pdf", columnasCertificado)}) AS RutaCertificadoPdf,
                        {ColumnaFecha("c", "fecha_emision", columnasCertificado)} AS FechaEmisionCertificado,
                        COALESCE({ColumnaFecha("c", "updated_at", columnasCertificado)}, {ColumnaFecha("c", "created_at", columnasCertificado)}, {ColumnaFecha("c", "fecha_emision", columnasCertificado)}) AS FechaActualizacionCertificado,
                        COALESCE(
                            NULLIF(TRIM(COALESCE({ColumnaTexto("c", "emitido_por", columnasCertificado)}, '')), ''),
                            NULLIF(TRIM(COALESCE({ColumnaTexto("c", "firmado_por", columnasCertificado)}, '')), '')
                        ) AS EmitidoPor,
                        COALESCE(
                            NULLIF(TRIM(COALESCE({ColumnaTexto("c", "aprobado_por", columnasCertificado)}, '')), ''),
                            NULLIF(TRIM(COALESCE({ColumnaTexto("c", "firmado_por", columnasCertificado)}, '')), '')
                        ) AS AprobadoPor
                    FROM public.aocr_tbcertificado c
                    WHERE c.codigo_solicitud = sb.SolicitudId
                    ORDER BY COALESCE({ColumnaFecha("c", "updated_at", columnasCertificado)}, {ColumnaFecha("c", "created_at", columnasCertificado)}, {ColumnaFecha("c", "fecha_emision", columnasCertificado)}) DESC NULLS LAST, {ColumnaEntera("c", "codigo_certificado", columnasCertificado)} DESC
                    LIMIT 1
                ) cert ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        {ColumnaEntera("fd", "codigo_firma", columnasFirma)} AS FirmaReconocimientoId,
                        NULLIF(TRIM(COALESCE({ColumnaTexto("fd", "numero_aocr", columnasFirma)}, '')), '') AS NumeroAocrReconocimiento,
                        {ColumnaTexto("fd", "ruta_documento", columnasFirma)} AS RutaReconocimientoFirmado,
                        NULLIF(TRIM(COALESCE({ColumnaTexto("fd", "nombre_firmante", columnasFirma)}, '')), '') AS NombreFirmanteReconocimiento,
                        NULLIF(TRIM(COALESCE({ColumnaTexto("fd", "cargo_firmante", columnasFirma)}, '')), '') AS CargoFirmanteReconocimiento,
                        {ColumnaFecha("fd", "fecha_firma", columnasFirma)} AS FechaFirmaReconocimiento
                    FROM public.aocr_tbfirma_documento fd
                    WHERE fd.codigo_solicitud = sb.SolicitudId
                      AND UPPER(COALESCE(fd.tipo_documento, '')) = 'RECONOCIMIENTO'
                    ORDER BY {ColumnaFecha("fd", "fecha_firma", columnasFirma)} DESC NULLS LAST, {ColumnaEntera("fd", "codigo_firma", columnasFirma)} DESC
                    LIMIT 1
                ) frec ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        {ColumnaEntera("fd", "codigo_firma", columnasFirma)} AS FirmaCondicionesId,
                        {ColumnaTexto("fd", "ruta_documento", columnasFirma)} AS RutaCondicionesFirmado,
                        NULLIF(TRIM(COALESCE({ColumnaTexto("fd", "nombre_firmante", columnasFirma)}, '')), '') AS NombreFirmanteCondiciones,
                        NULLIF(TRIM(COALESCE({ColumnaTexto("fd", "cargo_firmante", columnasFirma)}, '')), '') AS CargoFirmanteCondiciones,
                        {ColumnaFecha("fd", "fecha_firma", columnasFirma)} AS FechaFirmaCondiciones
                    FROM public.aocr_tbfirma_documento fd
                    WHERE fd.codigo_solicitud = sb.SolicitudId
                      AND UPPER(COALESCE(fd.tipo_documento, '')) = 'CONDICIONES_LIMITACIONES'
                    ORDER BY {ColumnaFecha("fd", "fecha_firma", columnasFirma)} DESC NULLS LAST, {ColumnaEntera("fd", "codigo_firma", columnasFirma)} DESC
                    LIMIT 1
                ) fcond ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        {ColumnaTexto("d", "ruta_guardada", columnasDocumento)} AS RutaAocrGenerada,
                        {ColumnaFecha("d", "fecha_carga", columnasDocumento)} AS FechaAocrGenerada
                    FROM public.aocr_tbdocumento d
                    WHERE d.codigo_solicitud = sb.SolicitudId
                      AND UPPER(COALESCE(d.tipo_documento, '')) IN ('AOCR', 'AOCR_GENERADO', 'BORRADOR_AOCR')
                      AND COALESCE(d.tamano_bytes, 0) > 0
                    ORDER BY {ColumnaFecha("d", "fecha_carga", columnasDocumento)} DESC NULLS LAST, {ColumnaEntera("d", "codigo_documento", columnasDocumento)} DESC
                    LIMIT 1
                ) daocr ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        pe.estado_actual AS EstadoCentralRaw
                    FROM public.aocr_proceso_estado pe
                    WHERE pe.solicitud_id = sb.SolicitudId
                      AND pe.activo = TRUE
                    ORDER BY pe.id DESC
                    LIMIT 1
                ) pcent ON TRUE
                WHERE cert.CertificadoId IS NOT NULL
                    OR frec.FirmaReconocimientoId IS NOT NULL
                    OR fcond.FirmaCondicionesId IS NOT NULL
                    OR daocr.RutaAocrGenerada IS NOT NULL
                    OR {NormalizarEstadoSql("pcent.EstadoCentralRaw")} IN ({estadosBandejaGeneradasFirmadasSql})
                    OR {estadoSolicitudNormalizadoSql} IN ({estadosBandejaGeneradasFirmadasSql})
                    OR {estadoCertificadoNormalizadoSql} IN ({estadosBandejaGeneradasFirmadasSql})
                    OR {estadoInformeNormalizadoSql} IN ({estadosBandejaGeneradasFirmadasSql})
                ORDER BY COALESCE(
                    frec.FechaFirmaReconocimiento,
                    fcond.FechaFirmaCondiciones,
                    cert.FechaActualizacionCertificado,
                    daocr.FechaAocrGenerada,
                    inf.FechaEnvioInformeDirdac,
                    insp.FechaProgramadaInspeccion,
                    sb.FechaSolicitud
                ) DESC NULLS LAST,
                sb.SolicitudId DESC;";

                return cn.Query<AocrBandejaDocumentoRow>(sql).AsList();
            }
        }

        private static HashSet<string> ObtenerColumnasTabla(NpgsqlConnection cn, string tabla)
        {
            const string sql = @"
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @tabla;";

            return new HashSet<string>(
                cn.Query<string>(sql, new { tabla })
                  .Where(c => !string.IsNullOrWhiteSpace(c))
                  .Select(c => c.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string ColumnaTexto(string alias, string columna, HashSet<string> columnas)
        {
            return columnas.Contains(columna) ? $"{alias}.{columna}" : "NULL::text";
        }

        private static string ColumnaEntera(string alias, string columna, HashSet<string> columnas)
        {
            return columnas.Contains(columna) ? $"{alias}.{columna}" : "NULL::integer";
        }

        private static string ColumnaFecha(string alias, string columna, HashSet<string> columnas)
        {
            return columnas.Contains(columna) ? $"{alias}.{columna}" : "NULL::timestamp";
        }

        private static string ColumnaBoolean(string alias, string columna, HashSet<string> columnas)
        {
            return columnas.Contains(columna) ? $"{alias}.{columna}" : "NULL::boolean";
        }

        private static string NormalizarEstadoSql(string expression)
        {
            return $@"TRIM(
                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                REPLACE(
                                    REPLACE(
                                        REPLACE(UPPER(COALESCE({expression}, '')), '_', ' '),
                                    '/', ' '),
                                'Á', 'A'),
                            'É', 'E'),
                        'Í', 'I'),
                    'Ó', 'O'),
                'Ú', 'U'))";
        }

        private static string FormatearListaSql(IEnumerable<string> valores)
        {
            return string.Join(", ",
                (valores ?? Enumerable.Empty<string>())
                    .Where(valor => !string.IsNullOrWhiteSpace(valor))
                    .Select(valor => valor.Trim().Replace("'", "''"))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(valor => $"'{valor}'"));
        }
    }
}
