using System.Collections.Generic;
using System.Configuration;
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
            const string sql = @"
                WITH solicitud_base AS (
                    SELECT
                        s.codigo_solicitud AS SolicitudId,
                        s.numero_solicitud AS NumeroSolicitud,
                        s.fecha_solicitud AS FechaSolicitud,
                        s.tipo_solicitud AS TipoSolicitud,
                        s.estado AS EstadoSolicitudRaw,
                        COALESCE(
                            NULLIF(TRIM(COALESCE(s.nombre_explotador, '')), ''),
                            NULLIF(TRIM(COALESCE(s.razon_social, '')), ''),
                            NULLIF(TRIM(COALESCE(s.razon_social_operador, '')), ''),
                            NULLIF(TRIM(COALESCE(s.nombre_comercial, '')), ''),
                            NULLIF(TRIM(COALESCE(s.nombre_operador, '')), ''),
                            'SIN OPERADOR'
                        ) AS NombreExplotador,
                        NULLIF(TRIM(COALESCE(s.numero_aoc, '')), '') AS NumeroAocBase,
                        COALESCE(s.companias_seleccionadas, '') AS CompaniasSeleccionadas,
                        COALESCE(s.codigo_usuario, 0) AS CodigoUsuario,
                        s.codigo_tecnico AS CodigoInspectorSolicitud,
                        NULLIF(TRIM(COALESCE(s.tecnico_responsable_nombre, '')), '') AS InspectorNombreSolicitud,
                        NULLIF(TRIM(COALESCE(s.inspector_apoyo_nombre, '')), '') AS InspectorApoyoNombreSolicitud
                    FROM public.aocr_tbsolicitud s
                    WHERE s.deleted_at IS NULL
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
                        i.codigo_inspeccion AS InspeccionId,
                        i.numero_inspeccion AS NumeroInspeccion,
                        i.estado AS EstadoInspeccionRaw,
                        i.resultado AS ResultadoInspeccionRaw,
                        i.codigo_inspector AS CodigoInspectorInspeccion,
                        COALESCE(
                            NULLIF(TRIM(COALESCE(i.inspector_principal_nombre, '')), ''),
                            NULLIF(TRIM(COALESCE(i.inspector_principal, '')), '')
                        ) AS InspectorPrincipalNombreInspeccion,
                        i.fecha_programada AS FechaProgramadaInspeccion
                    FROM public.aocr_tbinspeccion i
                    WHERE i.codigo_solicitud = sb.SolicitudId
                    ORDER BY i.fecha_programada DESC NULLS LAST, i.codigo_inspeccion DESC
                    LIMIT 1
                ) insp ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        inf.codigo_informe AS InformeId,
                        inf.estado_informe AS EstadoInformeTecnicoRaw,
                        inf.resultado AS ResultadoTecnicoFinalRaw,
                        inf.firmado_inspector AS InformeFirmadoInspector,
                        inf.firmado_dirdac AS InformeFirmadoDirdac,
                        inf.ruta_pdf AS RutaInformePdf,
                        inf.ruta_documento_firmado AS RutaInformeFirmadoPdf,
                        inf.fecha_firma_1 AS FechaFirmaInformeInspector,
                        inf.fecha_firma_2 AS FechaFirmaInformeDireccion,
                        inf.fecha_envio_dirdac AS FechaEnvioInformeDirdac
                    FROM public.aocr_tbinforme_inspeccion inf
                    WHERE inf.codigo_inspeccion = insp.InspeccionId
                    ORDER BY inf.version DESC, inf.codigo_informe DESC
                    LIMIT 1
                ) inf ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        c.codigo_certificado AS CertificadoId,
                        NULLIF(TRIM(COALESCE(c.numero_certificado, '')), '') AS NumeroAocrCertificado,
                        c.estado AS EstadoCertificadoRaw,
                        c.ruta_documento AS RutaCertificadoPdf,
                        c.fecha_emision AS FechaEmisionCertificado,
                        c.updated_at AS FechaActualizacionCertificado,
                        NULLIF(TRIM(COALESCE(c.emitido_por, '')), '') AS EmitidoPor,
                        NULLIF(TRIM(COALESCE(c.aprobado_por, '')), '') AS AprobadoPor
                    FROM public.aocr_tbcertificado c
                    WHERE c.codigo_solicitud = sb.SolicitudId
                    ORDER BY COALESCE(c.updated_at, c.created_at, c.fecha_emision) DESC NULLS LAST, c.codigo_certificado DESC
                    LIMIT 1
                ) cert ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        fd.codigo_firma AS FirmaReconocimientoId,
                        NULLIF(TRIM(COALESCE(fd.numero_aocr, '')), '') AS NumeroAocrReconocimiento,
                        fd.ruta_documento AS RutaReconocimientoFirmado,
                        NULLIF(TRIM(COALESCE(fd.nombre_firmante, '')), '') AS NombreFirmanteReconocimiento,
                        NULLIF(TRIM(COALESCE(fd.cargo_firmante, '')), '') AS CargoFirmanteReconocimiento,
                        fd.fecha_firma AS FechaFirmaReconocimiento
                    FROM public.aocr_tbfirma_documento fd
                    WHERE fd.codigo_solicitud = sb.SolicitudId
                      AND UPPER(COALESCE(fd.tipo_documento, '')) = 'RECONOCIMIENTO'
                    ORDER BY fd.fecha_firma DESC NULLS LAST, fd.codigo_firma DESC
                    LIMIT 1
                ) frec ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        fd.codigo_firma AS FirmaCondicionesId,
                        fd.ruta_documento AS RutaCondicionesFirmado,
                        NULLIF(TRIM(COALESCE(fd.nombre_firmante, '')), '') AS NombreFirmanteCondiciones,
                        NULLIF(TRIM(COALESCE(fd.cargo_firmante, '')), '') AS CargoFirmanteCondiciones,
                        fd.fecha_firma AS FechaFirmaCondiciones
                    FROM public.aocr_tbfirma_documento fd
                    WHERE fd.codigo_solicitud = sb.SolicitudId
                      AND UPPER(COALESCE(fd.tipo_documento, '')) = 'CONDICIONES_LIMITACIONES'
                    ORDER BY fd.fecha_firma DESC NULLS LAST, fd.codigo_firma DESC
                    LIMIT 1
                ) fcond ON TRUE
                LEFT JOIN LATERAL (
                    SELECT
                        d.ruta_guardada AS RutaAocrGenerada,
                        d.fecha_carga AS FechaAocrGenerada
                    FROM public.aocr_tbdocumento d
                    WHERE d.codigo_solicitud = sb.SolicitudId
                      AND UPPER(COALESCE(d.tipo_documento, '')) IN ('AOCR', 'AOCR_GENERADO', 'BORRADOR_AOCR')
                      AND COALESCE(d.tamano_bytes, 0) > 0
                    ORDER BY d.fecha_carga DESC NULLS LAST, d.codigo_documento DESC
                    LIMIT 1
                ) daocr ON TRUE
                WHERE cert.CertificadoId IS NOT NULL
                    OR frec.FirmaReconocimientoId IS NOT NULL
                    OR fcond.FirmaCondicionesId IS NOT NULL
                    OR daocr.RutaAocrGenerada IS NOT NULL
                    OR UPPER(COALESCE(sb.EstadoSolicitudRaw, '')) IN (
                        'GENERADO_CONDICIONES_LIMITACIONES',
                        'EN_REVISION_COORDINADOR_FINAL',
                        'ENVIADO_DCAV',
                        'FIRMADO_DCAV',
                        'FINALIZADO',
                        'AOCR_EN_ELABORACION',
                        'AOCR_EN_REVISION',
                        'AOCR_VALIDADO',
                        'AOCR_LEGALIZADO',
                        'AOCR_EMITIDO_RECIBIDO',
                        'ENVIADO_A_JEFATURA',
                        'EN REVISION COORDINADOR',
                        'ENVIADO_COORDINADOR'
                    )
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

            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                return cn.Query<AocrBandejaDocumentoRow>(sql).AsList();
            }
        }
    }
}