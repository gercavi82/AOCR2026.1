using System;
using System.Collections.Generic;

namespace CapaDatos.Constants
{
    public static class AocrEstadosProceso
    {
        public const string OrdenRequerida = "ORDEN_REQUERIDA";
        public const string OrdenGenerada = "ORDEN_GENERADA";
        public const string SolicitudInspeccionGenerada = "SOLICITUD_INSPECCION_GENERADA";
        public const string SolicitudInspeccionCargada = "SOLICITUD_INSPECCION_CARGADA";
        public const string PagoRegistrado = "PAGO_REGISTRADO";
        public const string PagoEnRevision = "PAGO_EN_REVISION";
        public const string PagoAprobado = "PAGO_APROBADO";
        public const string PagoRechazado = "PAGO_RECHAZADO";
        public const string Fr3Pendiente = "FR3_PENDIENTE";
        public const string Fr3Vinculado = "FR3_VINCULADO";
        public const string SolicitudAocrHabilitada = "SOLICITUD_AOCR_HABILITADA";
        public const string SolicitudAocrEnBorrador = "SOLICITUD_AOCR_EN_BORRADOR";
        public const string SolicitudAocrEnviada = "SOLICITUD_AOCR_ENVIADA";
        public const string PendienteAsignacionInspector = "PENDIENTE_ASIGNACION_INSPECTOR";
        public const string InspectorAsignado = "INSPECTOR_ASIGNADO";
        public const string RevisionDocumental = "REVISION_DOCUMENTAL";
        public const string DocumentacionObservada = "DOCUMENTACION_OBSERVADA";
        public const string SubsanacionRequerida = "SUBSANACION_REQUERIDA";
        public const string SubsanacionEnviada = "SUBSANACION_ENVIADA";
        public const string DocumentacionAceptada = "DOCUMENTACION_ACEPTADA";
        public const string LvPendiente = "LV_PENDIENTE";
        public const string LvEnProceso = "LV_EN_PROCESO";
        public const string LvFinalizada = "LV_FINALIZADA";
        public const string LvFirmada = "LV_FIRMADA";
        public const string InformeTecnicoPendiente = "INFORME_TECNICO_PENDIENTE";
        public const string InformeTecnicoGenerado = "INFORME_TECNICO_GENERADO";
        public const string InformeTecnicoFirmado = "INFORME_TECNICO_FIRMADO";
        public const string InformeEnviadoDireccion = "INFORME_ENVIADO_DIRECCION";
        public const string InformeAprobadoDireccion = "INFORME_APROBADO_DIRECCION";
        public const string InformeDevueltoDireccion = "INFORME_DEVUELTO_DIRECCION";
        public const string InformeTecnicoFirmadoInspector = "INFORME_TECNICO_FIRMADO_INSPECTOR";
        public const string PendienteRevisionInformeDcav = "PENDIENTE_REVISION_INFORME_DCAV";
        public const string InformeTecnicoObservadoDcav = "INFORME_TECNICO_OBSERVADO_DCAV";
        public const string InformeTecnicoAprobadoDcav = "INFORME_TECNICO_APROBADO_DCAV";
        public const string DocumentosHabilitadosInspector = "DOCUMENTOS_HABILITADOS_INSPECTOR";
        public const string DocumentosEnRevisionInspector = "DOCUMENTOS_EN_REVISION_INSPECTOR";
        public const string PendienteRevisionDocumentosDcav = "PENDIENTE_REVISION_DOCUMENTOS_DCAV";
        public const string DocumentosObservadosDcav = "DOCUMENTOS_OBSERVADOS_DCAV";
        public const string AprobadoDocumentosDcav = "APROBADO_DOCUMENTOS_DCAV";
        public const string PendienteRevisionDcav = "PENDIENTE_REVISION_DCAV";
        public const string ObservadoPorDcav = "OBSERVADO_POR_DCAV";
        public const string AprobadoPorDcav = "APROBADO_POR_DCAV";
        public const string PendienteFirmaDirectorGeneralLegacy = "PENDIENTE_FIRMA_DIRECTOR_GENERAL";
        public const string PendienteFirmaDirectorGeneral = "PENDIENTE_FIRMA_DIRDAC";
        public const string PendienteFirmaDirdac = PendienteFirmaDirectorGeneral;
        public const string FirmadoDirectorGeneral = "FIRMADO_DIRECTOR_GENERAL";
        public const string AocrFirmadoDirdac = "AOCR_FIRMADO_DIRDAC";
        public const string CondicionesFirmadasDirdac = "CONDICIONES_FIRMADAS_DIRDAC";
        public const string DocumentosFirmadosDirdac = "DOCUMENTOS_FIRMADOS_DIRDAC";
        public const string AocrDatosPendientes = "AOCR_DATOS_PENDIENTES";
        public const string AocrDatosCompletos = "AOCR_DATOS_COMPLETOS";
        public const string AocrPdfGenerado = "AOCR_PDF_GENERADO";
        public const string AocrFirmado = "AOCR_FIRMADO";
        public const string CondicionesPdfGenerado = "CONDICIONES_PDF_GENERADO";
        public const string CondicionesFirmadas = "CONDICIONES_FIRMADAS";
        public const string DocumentosFinalesLiberadosRt = "DOCUMENTOS_FINALES_LIBERADOS_RT";
        public const string AocrFinalizado = "AOCR_FINALIZADO";
        public const string AocrAnulado = "AOCR_ANULADO";

        public static readonly ISet<string> EstadosFinales = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AocrFinalizado,
            AocrAnulado
        };
    }
}
