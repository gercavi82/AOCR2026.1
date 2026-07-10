using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class AocrEstadoProcesoResult
    {
        public bool Ok { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Motivo { get; set; }
        public AocrProcesoEstadoRecord EstadoActual { get; set; }
    }

    public interface IAocrEstadoProcesoService
    {
        AocrProcesoEstadoRecord ObtenerEstadoActual(int solicitudId);
        AocrEstadoProcesoResult CambiarEstado(
            int solicitudId,
            string estadoNuevo,
            string accion,
            int usuarioId,
            string rolUsuario,
            string observacion = null,
            int? ordenRecaudacionId = null,
            int? inspeccionId = null,
            int? informeId = null);
        AocrEstadoProcesoResult SincronizarDesdeFuentesActuales(
            int solicitudId,
            string accion,
            int usuarioId,
            string rolUsuario,
            string observacion = null,
            int? ordenRecaudacionId = null,
            int? inspeccionId = null,
            int? informeId = null);
        bool PuedeEjecutarAccion(int solicitudId, string accion, string rolUsuario);
        string ObtenerSiguientePaso(int solicitudId, string rolUsuario);
        IReadOnlyList<string> ObtenerAccionesPermitidas(int solicitudId, string rolUsuario);
    }

    public sealed class AocrEstadoProcesoService : IAocrEstadoProcesoService
    {
        private readonly AocrProcesoEstadoDAO _procesoEstadoDao;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly InspeccionInformeDAO _informeDao;
        private readonly ListaVerificacionOperacionalEaeDAO _lvDao;
        private readonly AocrDocumentoGeneradoDAO _documentoGeneradoDao;
        private readonly AocrFirmaDocumentoDAO _firmaDocumentoDao;
        private readonly IAocrEstadoService _estadoService;

        private static readonly Dictionary<string, string[]> AllowedTransitions =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { AocrEstadosProceso.OrdenRequerida, new[] { AocrEstadosProceso.OrdenGenerada } },
                { AocrEstadosProceso.OrdenGenerada, new[] { AocrEstadosProceso.SolicitudInspeccionGenerada, AocrEstadosProceso.PagoRegistrado, AocrEstadosProceso.PagoEnRevision } },
                { AocrEstadosProceso.SolicitudInspeccionGenerada, new[] { AocrEstadosProceso.SolicitudInspeccionCargada } },
                { AocrEstadosProceso.SolicitudInspeccionCargada, new[] { AocrEstadosProceso.PagoRegistrado } },
                { AocrEstadosProceso.PagoRegistrado, new[] { AocrEstadosProceso.PagoEnRevision, AocrEstadosProceso.PagoAprobado, AocrEstadosProceso.PagoRechazado } },
                { AocrEstadosProceso.PagoEnRevision, new[] { AocrEstadosProceso.PagoAprobado, AocrEstadosProceso.PagoRechazado } },
                { AocrEstadosProceso.PagoAprobado, new[] { AocrEstadosProceso.Fr3Pendiente, AocrEstadosProceso.Fr3Vinculado, AocrEstadosProceso.SolicitudAocrHabilitada } },
                { AocrEstadosProceso.PagoRechazado, new[] { AocrEstadosProceso.PagoRegistrado } },
                { AocrEstadosProceso.Fr3Pendiente, new[] { AocrEstadosProceso.Fr3Vinculado } },
                { AocrEstadosProceso.Fr3Vinculado, new[] { AocrEstadosProceso.SolicitudAocrHabilitada } },
                { AocrEstadosProceso.SolicitudAocrHabilitada, new[] { AocrEstadosProceso.SolicitudAocrEnBorrador, AocrEstadosProceso.SolicitudAocrEnviada } },
                { AocrEstadosProceso.SolicitudAocrEnBorrador, new[] { AocrEstadosProceso.SolicitudAocrEnviada } },
                { AocrEstadosProceso.SolicitudAocrEnviada, new[] { AocrEstadosProceso.PendienteAsignacionInspector, AocrEstadosProceso.DocumentacionObservada } },
                { AocrEstadosProceso.PendienteAsignacionInspector, new[] { AocrEstadosProceso.InspectorAsignado } },
                { AocrEstadosProceso.InspectorAsignado, new[] { AocrEstadosProceso.RevisionDocumental } },
                { AocrEstadosProceso.RevisionDocumental, new[] { AocrEstadosProceso.DocumentacionObservada, AocrEstadosProceso.SubsanacionRequerida, AocrEstadosProceso.DocumentacionAceptada } },
                { AocrEstadosProceso.DocumentacionObservada, new[] { AocrEstadosProceso.SubsanacionRequerida, AocrEstadosProceso.SubsanacionEnviada } },
                { AocrEstadosProceso.SubsanacionRequerida, new[] { AocrEstadosProceso.SubsanacionEnviada } },
                { AocrEstadosProceso.SubsanacionEnviada, new[] { AocrEstadosProceso.RevisionDocumental, AocrEstadosProceso.DocumentacionAceptada } },
                { AocrEstadosProceso.DocumentacionAceptada, new[] { AocrEstadosProceso.LvPendiente, AocrEstadosProceso.LvEnProceso, AocrEstadosProceso.InformeTecnicoPendiente } },
                { AocrEstadosProceso.LvPendiente, new[] { AocrEstadosProceso.LvEnProceso, AocrEstadosProceso.LvFinalizada } },
                { AocrEstadosProceso.LvEnProceso, new[] { AocrEstadosProceso.LvFinalizada } },
                { AocrEstadosProceso.LvFinalizada, new[] { AocrEstadosProceso.LvFirmada } },
                { AocrEstadosProceso.LvFirmada, new[] { AocrEstadosProceso.InformeTecnicoPendiente, AocrEstadosProceso.InformeTecnicoGenerado } },
                { AocrEstadosProceso.InformeTecnicoPendiente, new[] { AocrEstadosProceso.InformeTecnicoGenerado } },
                { AocrEstadosProceso.InformeTecnicoGenerado, new[] { AocrEstadosProceso.InformeTecnicoFirmado, AocrEstadosProceso.InformeTecnicoFirmadoInspector } },
                { AocrEstadosProceso.InformeTecnicoFirmado, new[] { AocrEstadosProceso.InformeTecnicoFirmadoInspector, AocrEstadosProceso.PendienteRevisionInformeDcav, AocrEstadosProceso.InformeEnviadoDireccion, AocrEstadosProceso.InformeAprobadoDireccion } },
                { AocrEstadosProceso.InformeTecnicoFirmadoInspector, new[] { AocrEstadosProceso.PendienteRevisionInformeDcav } },
                { AocrEstadosProceso.PendienteRevisionInformeDcav, new[] { AocrEstadosProceso.InformeTecnicoAprobadoDcav, AocrEstadosProceso.InformeTecnicoObservadoDcav } },
                { AocrEstadosProceso.InformeTecnicoObservadoDcav, new[] { AocrEstadosProceso.InformeTecnicoPendiente, AocrEstadosProceso.InformeTecnicoGenerado, AocrEstadosProceso.PendienteRevisionInformeDcav } },
                { AocrEstadosProceso.InformeTecnicoAprobadoDcav, new[] { AocrEstadosProceso.DocumentosHabilitadosInspector } },
                { AocrEstadosProceso.DocumentosHabilitadosInspector, new[] { AocrEstadosProceso.DocumentosEnRevisionInspector, AocrEstadosProceso.PendienteRevisionDocumentosDcav } },
                { AocrEstadosProceso.DocumentosEnRevisionInspector, new[] { AocrEstadosProceso.PendienteRevisionDocumentosDcav } },
                { AocrEstadosProceso.PendienteRevisionDocumentosDcav, new[] { AocrEstadosProceso.DocumentosObservadosDcav, AocrEstadosProceso.AprobadoDocumentosDcav } },
                { AocrEstadosProceso.DocumentosObservadosDcav, new[] { AocrEstadosProceso.DocumentosEnRevisionInspector, AocrEstadosProceso.PendienteRevisionDocumentosDcav } },
                { AocrEstadosProceso.AprobadoDocumentosDcav, new[] { AocrEstadosProceso.PendienteFirmaDirectorGeneral } },
                { AocrEstadosProceso.InformeEnviadoDireccion, new[] { AocrEstadosProceso.InformeAprobadoDireccion, AocrEstadosProceso.InformeDevueltoDireccion } },
                { AocrEstadosProceso.InformeDevueltoDireccion, new[] { AocrEstadosProceso.InformeTecnicoPendiente, AocrEstadosProceso.InformeTecnicoGenerado } },
                { AocrEstadosProceso.InformeAprobadoDireccion, new[] { AocrEstadosProceso.AocrDatosPendientes, AocrEstadosProceso.AocrDatosCompletos, AocrEstadosProceso.PendienteRevisionDcav, AocrEstadosProceso.DocumentosHabilitadosInspector } },
                { AocrEstadosProceso.AocrDatosPendientes, new[] { AocrEstadosProceso.AocrDatosCompletos, AocrEstadosProceso.AocrPdfGenerado } },
                { AocrEstadosProceso.AocrDatosCompletos, new[] { AocrEstadosProceso.AocrPdfGenerado } },
                { AocrEstadosProceso.AocrPdfGenerado, new[] { AocrEstadosProceso.AocrFirmado, AocrEstadosProceso.PendienteRevisionDcav } },
                { AocrEstadosProceso.PendienteRevisionDcav, new[] { AocrEstadosProceso.AprobadoPorDcav, AocrEstadosProceso.ObservadoPorDcav, AocrEstadosProceso.PendienteRevisionDocumentosDcav } },
                { AocrEstadosProceso.ObservadoPorDcav, new[] { AocrEstadosProceso.InformeTecnicoPendiente, AocrEstadosProceso.InformeTecnicoGenerado, AocrEstadosProceso.AocrDatosPendientes, AocrEstadosProceso.PendienteRevisionDcav } },
                { AocrEstadosProceso.AprobadoPorDcav, new[] { AocrEstadosProceso.PendienteFirmaDirectorGeneral } },
                { AocrEstadosProceso.PendienteFirmaDirectorGeneral, new[] { AocrEstadosProceso.AocrFirmado, AocrEstadosProceso.AocrFirmadoDirdac, AocrEstadosProceso.CondicionesFirmadasDirdac, AocrEstadosProceso.FirmadoDirectorGeneral, AocrEstadosProceso.ObservadoPorDcav } },
                { AocrEstadosProceso.PendienteFirmaDirectorGeneralLegacy, new[] { AocrEstadosProceso.AocrFirmado, AocrEstadosProceso.AocrFirmadoDirdac, AocrEstadosProceso.CondicionesFirmadasDirdac, AocrEstadosProceso.FirmadoDirectorGeneral, AocrEstadosProceso.ObservadoPorDcav } },
                { AocrEstadosProceso.AocrFirmadoDirdac, new[] { AocrEstadosProceso.CondicionesFirmadasDirdac, AocrEstadosProceso.DocumentosFirmadosDirdac } },
                { AocrEstadosProceso.CondicionesFirmadasDirdac, new[] { AocrEstadosProceso.AocrFirmadoDirdac, AocrEstadosProceso.DocumentosFirmadosDirdac } },
                { AocrEstadosProceso.DocumentosFirmadosDirdac, new[] { AocrEstadosProceso.DocumentosFinalesLiberadosRt, AocrEstadosProceso.AocrFinalizado } },
                { AocrEstadosProceso.FirmadoDirectorGeneral, new[] { AocrEstadosProceso.AocrFirmado, AocrEstadosProceso.CondicionesFirmadas, AocrEstadosProceso.DocumentosFinalesLiberadosRt, AocrEstadosProceso.AocrFinalizado } },
                { AocrEstadosProceso.AocrFirmado, new[] { AocrEstadosProceso.CondicionesPdfGenerado, AocrEstadosProceso.CondicionesFirmadas } },
                { AocrEstadosProceso.CondicionesPdfGenerado, new[] { AocrEstadosProceso.CondicionesFirmadas } },
                { AocrEstadosProceso.CondicionesFirmadas, new[] { AocrEstadosProceso.DocumentosFinalesLiberadosRt, AocrEstadosProceso.AocrFinalizado } },
                { AocrEstadosProceso.DocumentosFinalesLiberadosRt, new[] { AocrEstadosProceso.AocrFinalizado } }
            };

        private static readonly Dictionary<string, EstadoMetadata> MetadataByState =
            new Dictionary<string, EstadoMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                { AocrEstadosProceso.OrdenRequerida, Meta("RECAUDACION", "Solicitante", "Crear orden", "CREAR_ORDEN") },
                { AocrEstadosProceso.OrdenGenerada, Meta("RECAUDACION", "Solicitante", "Registrar pago", "REGISTRAR_PAGO") },
                { AocrEstadosProceso.SolicitudInspeccionGenerada, Meta("RECAUDACION", "Solicitante", "Cargar solicitud de inspeccion", "CARGAR_SOLICITUD_INSPECCION") },
                { AocrEstadosProceso.SolicitudInspeccionCargada, Meta("RECAUDACION", "Solicitante", "Registrar pago", "REGISTRAR_PAGO") },
                { AocrEstadosProceso.PagoRegistrado, Meta("PAGO", "Financiero", "Revisar comprobante", "REVISAR_PAGO") },
                { AocrEstadosProceso.PagoEnRevision, Meta("PAGO", "Financiero", "Aprobar o rechazar pago", "APROBAR_PAGO") },
                { AocrEstadosProceso.PagoAprobado, Meta("PAGO", "Financiero", "Vincular FR3", "VINCULAR_FR3") },
                { AocrEstadosProceso.PagoRechazado, Meta("PAGO", "Solicitante", "Subir nuevo comprobante", "SUBIR_COMPROBANTE") },
                { AocrEstadosProceso.Fr3Pendiente, Meta("FR3", "Financiero", "Generar o sincronizar FR3", "GENERAR_FR3") },
                { AocrEstadosProceso.Fr3Vinculado, Meta("FR3", "Solicitante", "Continuar solicitud AOCR", "CONTINUAR_SOLICITUD_AOCR") },
                { AocrEstadosProceso.SolicitudAocrHabilitada, Meta("SOLICITUD_AOCR", "Solicitante", "Completar solicitud AOCR", "COMPLETAR_SOLICITUD_AOCR") },
                { AocrEstadosProceso.SolicitudAocrEnBorrador, Meta("SOLICITUD_AOCR", "Solicitante", "Enviar solicitud AOCR", "ENVIAR_SOLICITUD_AOCR") },
                { AocrEstadosProceso.SolicitudAocrEnviada, Meta("COORDINACION", "Coordinacion", "Revisar solicitud", "REVISAR_SOLICITUD_AOCR") },
                { AocrEstadosProceso.PendienteAsignacionInspector, Meta("COORDINACION", "Coordinacion", "Asignar inspector", "ASIGNAR_INSPECTOR") },
                { AocrEstadosProceso.InspectorAsignado, Meta("INSPECCION", "InspectorTecnico", "Abrir revision documental", "ABRIR_REVISION_DOCUMENTAL") },
                { AocrEstadosProceso.RevisionDocumental, Meta("REVISION_DOCUMENTAL", "InspectorTecnico", "Revisar documentacion", "REVISAR_DOCUMENTACION") },
                { AocrEstadosProceso.DocumentacionObservada, Meta("REVISION_DOCUMENTAL", "Solicitante", "Atender observaciones", "ATENDER_OBSERVACIONES") },
                { AocrEstadosProceso.SubsanacionRequerida, Meta("REVISION_DOCUMENTAL", "Solicitante", "Cargar subsanacion", "CARGAR_SUBSANACION") },
                { AocrEstadosProceso.SubsanacionEnviada, Meta("REVISION_DOCUMENTAL", "InspectorTecnico", "Revisar subsanacion", "REVISAR_SUBSANACION") },
                { AocrEstadosProceso.DocumentacionAceptada, Meta("LV_EAE", "InspectorTecnico", "Iniciar LV/EAE", "INICIAR_LV_EAE") },
                { AocrEstadosProceso.LvPendiente, Meta("LV_EAE", "InspectorTecnico", "Iniciar LV/EAE", "INICIAR_LV_EAE") },
                { AocrEstadosProceso.LvEnProceso, Meta("LV_EAE", "InspectorTecnico", "Finalizar LV/EAE", "FINALIZAR_LV_EAE") },
                { AocrEstadosProceso.LvFinalizada, Meta("LV_EAE", "InspectorTecnico", "Firmar LV/EAE", "FIRMAR_LV") },
                { AocrEstadosProceso.LvFirmada, Meta("INFORME_TECNICO", "InspectorTecnico", "Generar informe tecnico", "GENERAR_INFORME_TECNICO") },
                { AocrEstadosProceso.InformeTecnicoPendiente, Meta("INFORME_TECNICO", "InspectorTecnico", "Generar informe tecnico", "GENERAR_INFORME_TECNICO") },
                { AocrEstadosProceso.InformeTecnicoGenerado, Meta("INFORME_TECNICO", "InspectorTecnico", "Firmar informe tecnico", "FIRMAR_INFORME_TECNICO") },
                { AocrEstadosProceso.InformeTecnicoFirmado, Meta("INFORME_TECNICO", "DirectorCertificacionesDcav", "Revisar informe tecnico", "DCAV_REVISAR_INFORME") },
                { AocrEstadosProceso.InformeTecnicoFirmadoInspector, Meta("INFORME_TECNICO", "DirectorCertificacionesDcav", "Revisar informe tecnico", "DCAV_REVISAR_INFORME") },
                { AocrEstadosProceso.PendienteRevisionInformeDcav, Meta("REVISION_INFORME_DCAV", "DirectorCertificacionesDcav", "Aprobar o devolver informe tecnico", "DCAV_REVISAR_INFORME") },
                { AocrEstadosProceso.InformeTecnicoObservadoDcav, Meta("INFORME_TECNICO", "InspectorTecnico", "Corregir informe tecnico", "AJUSTAR_INFORME_TECNICO") },
                { AocrEstadosProceso.InformeTecnicoAprobadoDcav, Meta("DOCUMENTOS_AOCR", "InspectorTecnico", "Revisar AOCR y Condiciones", "REVISAR_AOCR_CONDICIONES") },
                { AocrEstadosProceso.DocumentosHabilitadosInspector, Meta("DOCUMENTOS_AOCR", "InspectorTecnico", "Revisar AOCR y Condiciones", "REVISAR_AOCR_CONDICIONES") },
                { AocrEstadosProceso.DocumentosEnRevisionInspector, Meta("DOCUMENTOS_AOCR", "InspectorTecnico", "Finalizar revision y enviar a DCAV", "ENVIAR_DOCUMENTOS_DCAV") },
                { AocrEstadosProceso.PendienteRevisionDocumentosDcav, Meta("REVISION_DOCUMENTOS_DCAV", "DirectorCertificacionesDcav", "Aprobar o devolver AOCR y Condiciones", "DCAV_REVISAR_DOCUMENTOS") },
                { AocrEstadosProceso.DocumentosObservadosDcav, Meta("DOCUMENTOS_AOCR", "InspectorTecnico", "Corregir documentos observados", "AJUSTAR_AOCR_CONDICIONES") },
                { AocrEstadosProceso.AprobadoDocumentosDcav, Meta("FIRMA_DIRECTOR_GENERAL", "DirectorGeneral", "Firmar AOCR y Condiciones", "FIRMAR_AOCR_CONDICIONES") },
                { AocrEstadosProceso.InformeEnviadoDireccion, Meta("DIRECCION", "DireccionJefaturaTecnica", "Aprobar o devolver informe tecnico", "APROBAR_INFORME_TECNICO") },
                { AocrEstadosProceso.InformeAprobadoDireccion, Meta("DIRECCION", "DireccionJefaturaTecnica", "Completar datos AOCR", "COMPLETAR_AOCR") },
                { AocrEstadosProceso.InformeDevueltoDireccion, Meta("DIRECCION", "InspectorTecnico", "Ajustar informe tecnico", "AJUSTAR_INFORME_TECNICO") },
                { AocrEstadosProceso.PendienteRevisionDcav, Meta("REVISION_DCAV", "DirectorCertificacionesDcav", "Revisar expediente AOCR", "DCAV_REVISAR") },
                { AocrEstadosProceso.ObservadoPorDcav, Meta("REVISION_DCAV", "Coordinacion", "Atender observaciones DCAV", "ATENDER_OBSERVACIONES_DCAV") },
                { AocrEstadosProceso.AprobadoPorDcav, Meta("REVISION_DCAV", "DirectorGeneral", "Enviar a firma institucional", "ENVIAR_FIRMA_DIRECTOR_GENERAL") },
                { AocrEstadosProceso.PendienteFirmaDirectorGeneral, Meta("FIRMA_DIRECTOR_GENERAL", "DirectorGeneral", "Firmar AOCR y Condiciones", "FIRMAR_AOCR_CONDICIONES") },
                { AocrEstadosProceso.PendienteFirmaDirectorGeneralLegacy, Meta("FIRMA_DIRECTOR_GENERAL", "DirectorGeneral", "Firmar AOCR y Condiciones", "FIRMAR_AOCR_CONDICIONES") },
                { AocrEstadosProceso.AocrFirmadoDirdac, Meta("FIRMA_DIRECTOR_GENERAL", "DirectorGeneral", "Firmar Condiciones", "FIRMAR_CONDICIONES") },
                { AocrEstadosProceso.CondicionesFirmadasDirdac, Meta("FIRMA_DIRECTOR_GENERAL", "DirectorGeneral", "Firmar AOCR", "FIRMAR_AOCR") },
                { AocrEstadosProceso.DocumentosFirmadosDirdac, Meta("BANDEJA_FINAL", "Solicitante", "Descargar documentos finales", "DESCARGAR_DOCUMENTOS_FINALES") },
                { AocrEstadosProceso.FirmadoDirectorGeneral, Meta("FIRMA_DIRECTOR_GENERAL", "Solicitante", "Descargar documentos finales", "DESCARGAR_DOCUMENTOS_FINALES") },
                { AocrEstadosProceso.AocrDatosPendientes, Meta("FIRMA_AOCR", "DireccionJefaturaTecnica", "Completar datos AOCR", "COMPLETAR_AOCR") },
                { AocrEstadosProceso.AocrDatosCompletos, Meta("FIRMA_AOCR", "DireccionJefaturaTecnica", "Generar PDF AOCR", "GENERAR_PDF_AOCR") },
                { AocrEstadosProceso.AocrPdfGenerado, Meta("FIRMA_AOCR", "DireccionJefaturaTecnica", "Firmar AOCR", "FIRMAR_AOCR") },
                { AocrEstadosProceso.AocrFirmado, Meta("CONDICIONES", "DireccionJefaturaTecnica", "Generar condiciones y limitaciones", "GENERAR_CONDICIONES") },
                { AocrEstadosProceso.CondicionesPdfGenerado, Meta("CONDICIONES", "DireccionJefaturaTecnica", "Firmar condiciones y limitaciones", "FIRMAR_CONDICIONES") },
                { AocrEstadosProceso.CondicionesFirmadas, Meta("BANDEJA_FINAL", "DireccionJefaturaTecnica", "Liberar documentos finales al RT", "LIBERAR_DOCUMENTOS_RT") },
                { AocrEstadosProceso.DocumentosFinalesLiberadosRt, Meta("BANDEJA_FINAL", "Solicitante", "Descargar documentos finales", "DESCARGAR_DOCUMENTOS_FINALES") },
                { AocrEstadosProceso.AocrFinalizado, Meta("CIERRE", "Solicitante", "Proceso finalizado", "SIN_ACCION") },
                { AocrEstadosProceso.AocrAnulado, Meta("CIERRE", "Administrador", "Proceso anulado", "SIN_ACCION") }
            };

        private static readonly Dictionary<string, string[]> AllowedActionsByRoleAndState =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { Key(AocrEstadosProceso.OrdenRequerida, "Solicitante"), new[] { "CREAR_ORDEN" } },
                { Key(AocrEstadosProceso.OrdenGenerada, "Solicitante"), new[] { "DESCARGAR_ORDEN", "REGISTRAR_PAGO" } },
                { Key(AocrEstadosProceso.PagoRegistrado, "Financiero"), new[] { "REVISAR_PAGO", "APROBAR_PAGO", "RECHAZAR_PAGO" } },
                { Key(AocrEstadosProceso.PagoEnRevision, "Financiero"), new[] { "REVISAR_PAGO", "APROBAR_PAGO", "RECHAZAR_PAGO" } },
                { Key(AocrEstadosProceso.PagoAprobado, "Financiero"), new[] { "VINCULAR_FR3", "SINCRONIZAR_FR3" } },
                { Key(AocrEstadosProceso.PagoRechazado, "Solicitante"), new[] { "SUBIR_COMPROBANTE" } },
                { Key(AocrEstadosProceso.SolicitudAocrHabilitada, "Solicitante"), new[] { "COMPLETAR_SOLICITUD_AOCR" } },
                { Key(AocrEstadosProceso.SolicitudAocrEnBorrador, "Solicitante"), new[] { "GUARDAR_BORRADOR", "ENVIAR_SOLICITUD_AOCR" } },
                { Key(AocrEstadosProceso.SolicitudAocrEnviada, "Coordinacion"), new[] { "REVISAR_SOLICITUD_AOCR", "ENVIAR_ASIGNACION" } },
                { Key(AocrEstadosProceso.PendienteAsignacionInspector, "Coordinacion"), new[] { "ASIGNAR_INSPECTOR" } },
                { Key(AocrEstadosProceso.InspectorAsignado, "InspectorTecnico"), new[] { "ABRIR_REVISION_DOCUMENTAL" } },
                { Key(AocrEstadosProceso.RevisionDocumental, "InspectorTecnico"), new[] { "ACEPTAR_DOCUMENTACION", "OBSERVAR_DOCUMENTACION", "SOLICITAR_SUBSANACION" } },
                { Key(AocrEstadosProceso.SubsanacionRequerida, "Solicitante"), new[] { "VER_OBSERVACIONES", "CARGAR_SUBSANACION" } },
                { Key(AocrEstadosProceso.SubsanacionEnviada, "InspectorTecnico"), new[] { "REVISAR_SUBSANACION" } },
                { Key(AocrEstadosProceso.DocumentacionAceptada, "InspectorTecnico"), new[] { "INICIAR_LV_EAE" } },
                { Key(AocrEstadosProceso.LvPendiente, "InspectorTecnico"), new[] { "INICIAR_LV_EAE", "GUARDAR_LV_EAE" } },
                { Key(AocrEstadosProceso.LvEnProceso, "InspectorTecnico"), new[] { "GUARDAR_LV_EAE", "FINALIZAR_LV_EAE" } },
                { Key(AocrEstadosProceso.LvFinalizada, "InspectorTecnico"), new[] { "FIRMAR_LV" } },
                { Key(AocrEstadosProceso.LvFirmada, "InspectorTecnico"), new[] { "GENERAR_INFORME_TECNICO" } },
                { Key(AocrEstadosProceso.InformeTecnicoGenerado, "InspectorTecnico"), new[] { "FIRMAR_INFORME_TECNICO" } },
                { Key(AocrEstadosProceso.InformeTecnicoFirmado, "DirectorCertificacionesDcav"), new[] { "DCAV_VER_EXPEDIENTE", "DCAV_APROBAR_INFORME", "DCAV_DEVOLVER_INFORME" } },
                { Key(AocrEstadosProceso.InformeTecnicoFirmadoInspector, "DirectorCertificacionesDcav"), new[] { "DCAV_VER_EXPEDIENTE", "DCAV_APROBAR_INFORME", "DCAV_DEVOLVER_INFORME" } },
                { Key(AocrEstadosProceso.PendienteRevisionInformeDcav, "DirectorCertificacionesDcav"), new[] { "DCAV_VER_EXPEDIENTE", "DCAV_APROBAR_INFORME", "DCAV_DEVOLVER_INFORME" } },
                { Key(AocrEstadosProceso.InformeTecnicoObservadoDcav, "InspectorTecnico"), new[] { "AJUSTAR_INFORME_TECNICO", "FIRMAR_INFORME_TECNICO" } },
                { Key(AocrEstadosProceso.InformeTecnicoAprobadoDcav, "InspectorTecnico"), new[] { "REVISAR_AOCR", "REVISAR_CONDICIONES", "ENVIAR_DOCUMENTOS_DCAV" } },
                { Key(AocrEstadosProceso.DocumentosHabilitadosInspector, "InspectorTecnico"), new[] { "REVISAR_AOCR", "REVISAR_CONDICIONES", "ENVIAR_DOCUMENTOS_DCAV" } },
                { Key(AocrEstadosProceso.DocumentosEnRevisionInspector, "InspectorTecnico"), new[] { "REVISAR_AOCR", "REVISAR_CONDICIONES", "ENVIAR_DOCUMENTOS_DCAV" } },
                { Key(AocrEstadosProceso.PendienteRevisionDocumentosDcav, "DirectorCertificacionesDcav"), new[] { "DCAV_VER_EXPEDIENTE", "DCAV_APROBAR_DOCUMENTOS", "DCAV_DEVOLVER_DOCUMENTOS" } },
                { Key(AocrEstadosProceso.DocumentosObservadosDcav, "InspectorTecnico"), new[] { "AJUSTAR_AOCR", "AJUSTAR_CONDICIONES", "ENVIAR_DOCUMENTOS_DCAV" } },
                { Key(AocrEstadosProceso.InformeEnviadoDireccion, "DireccionJefaturaTecnica"), new[] { "APROBAR_INFORME_TECNICO", "DEVOLVER_INFORME_TECNICO" } },
                { Key(AocrEstadosProceso.InformeAprobadoDireccion, "DireccionJefaturaTecnica"), new[] { "COMPLETAR_AOCR" } },
                { Key(AocrEstadosProceso.PendienteRevisionDcav, "DirectorCertificacionesDcav"), new[] { "DCAV_VER_EXPEDIENTE", "DCAV_APROBAR", "DCAV_DEVOLVER" } },
                { Key(AocrEstadosProceso.PendienteFirmaDirectorGeneral, "DirectorGeneral"), new[] { "FIRMAR_AOCR", "FIRMAR_CONDICIONES", "DEVOLVER_A_DCAV" } },
                { Key(AocrEstadosProceso.PendienteFirmaDirectorGeneralLegacy, "DirectorGeneral"), new[] { "FIRMAR_AOCR", "FIRMAR_CONDICIONES", "DEVOLVER_A_DCAV" } },
                { Key(AocrEstadosProceso.AocrFirmadoDirdac, "DirectorGeneral"), new[] { "FIRMAR_CONDICIONES" } },
                { Key(AocrEstadosProceso.CondicionesFirmadasDirdac, "DirectorGeneral"), new[] { "FIRMAR_AOCR" } },
                { Key(AocrEstadosProceso.AocrDatosPendientes, "DireccionJefaturaTecnica"), new[] { "GUARDAR_AOCR", "VALIDAR_AOCR" } },
                { Key(AocrEstadosProceso.AocrDatosCompletos, "DireccionJefaturaTecnica"), new[] { "GENERAR_PDF_AOCR" } },
                { Key(AocrEstadosProceso.AocrPdfGenerado, "DireccionJefaturaTecnica"), new[] { "FIRMAR_AOCR" } },
                { Key(AocrEstadosProceso.AocrFirmado, "DireccionJefaturaTecnica"), new[] { "GENERAR_CONDICIONES" } },
                { Key(AocrEstadosProceso.CondicionesPdfGenerado, "DireccionJefaturaTecnica"), new[] { "FIRMAR_CONDICIONES" } },
                { Key(AocrEstadosProceso.CondicionesFirmadas, "DireccionJefaturaTecnica"), new[] { "LIBERAR_DOCUMENTOS_RT" } },
                { Key(AocrEstadosProceso.DocumentosFinalesLiberadosRt, "Solicitante"), new[] { "DESCARGAR_AOCR", "DESCARGAR_CONDICIONES", "DESCARGAR_DOCUMENTOS_FINALES" } }
            };

        public AocrEstadoProcesoService()
            : this(
                new AocrProcesoEstadoDAO(),
                new SolicitudAOCRDAO(),
                new InspeccionDAO(),
                new InspeccionInformeDAO(),
                new ListaVerificacionOperacionalEaeDAO(),
                new AocrDocumentoGeneradoDAO(),
                new AocrFirmaDocumentoDAO(),
                new AocrEstadoService())
        {
        }

        public AocrEstadoProcesoService(
            AocrProcesoEstadoDAO procesoEstadoDao,
            SolicitudAOCRDAO solicitudDao,
            InspeccionDAO inspeccionDao,
            InspeccionInformeDAO informeDao,
            ListaVerificacionOperacionalEaeDAO lvDao,
            AocrDocumentoGeneradoDAO documentoGeneradoDao,
            AocrFirmaDocumentoDAO firmaDocumentoDao,
            IAocrEstadoService estadoService)
        {
            _procesoEstadoDao = procesoEstadoDao ?? new AocrProcesoEstadoDAO();
            _solicitudDao = solicitudDao ?? new SolicitudAOCRDAO();
            _inspeccionDao = inspeccionDao ?? new InspeccionDAO();
            _informeDao = informeDao ?? new InspeccionInformeDAO();
            _lvDao = lvDao ?? new ListaVerificacionOperacionalEaeDAO();
            _documentoGeneradoDao = documentoGeneradoDao ?? new AocrDocumentoGeneradoDAO();
            _firmaDocumentoDao = firmaDocumentoDao ?? new AocrFirmaDocumentoDAO();
            _estadoService = estadoService ?? new AocrEstadoService();
        }

        public AocrProcesoEstadoRecord ObtenerEstadoActual(int solicitudId)
        {
            var actual = _procesoEstadoDao.ObtenerActivoPorSolicitud(solicitudId);
            if (actual != null)
            {
                Trace.TraceInformation("[AOCR_ESTADO][GET] SolicitudId=" + solicitudId + "; EstadoActual=" + (actual.EstadoActual ?? string.Empty) + ";");
                return actual;
            }

            var sync = SincronizarDesdeFuentesActuales(solicitudId, "MIGRACION_INICIAL", 0, "SISTEMA", "Sincronizacion inicial del estado central.");
            Trace.TraceInformation("[AOCR_ESTADO][GET] SolicitudId=" + solicitudId + "; EstadoActual=" + (sync.EstadoActual != null ? sync.EstadoActual.EstadoActual : string.Empty) + ";");
            return sync.EstadoActual;
        }

        public AocrEstadoProcesoResult CambiarEstado(
            int solicitudId,
            string estadoNuevo,
            string accion,
            int usuarioId,
            string rolUsuario,
            string observacion = null,
            int? ordenRecaudacionId = null,
            int? inspeccionId = null,
            int? informeId = null)
        {
            var estadoDestino = NormalizeProcesoState(estadoNuevo);
            var actual = _procesoEstadoDao.ObtenerActivoPorSolicitud(solicitudId);
            var estadoAnterior = actual != null ? actual.EstadoActual : null;

            Trace.TraceInformation(
                "[AOCR_ESTADO][CAMBIO_IN] SolicitudId=" + solicitudId +
                "; EstadoAnterior=" + (estadoAnterior ?? string.Empty) +
                "; EstadoNuevo=" + (estadoDestino ?? string.Empty) +
                "; Accion=" + (accion ?? string.Empty) +
                "; Usuario=" + usuarioId +
                "; Rol=" + (rolUsuario ?? string.Empty) + ";");

            if (solicitudId <= 0 || string.IsNullOrWhiteSpace(estadoDestino))
            {
                return Deny(solicitudId, estadoAnterior, estadoDestino, "Parametros invalidos.");
            }

            if (!CanTransition(estadoAnterior, estadoDestino))
            {
                if (StrictValidationEnabled())
                {
                    return Deny(solicitudId, estadoAnterior, estadoDestino, "Transicion no permitida.");
                }
            }

            var metadata = ResolveMetadata(estadoDestino);
            var record = new AocrProcesoEstadoRecord
            {
                SolicitudId = solicitudId,
                OrdenRecaudacionId = ordenRecaudacionId ?? (actual != null ? actual.OrdenRecaudacionId : null),
                InspeccionId = inspeccionId ?? (actual != null ? actual.InspeccionId : null),
                InformeId = informeId ?? (actual != null ? actual.InformeId : null),
                EstadoActual = estadoDestino,
                EtapaActual = metadata.Etapa,
                RolResponsable = metadata.RolResponsable,
                UsuarioResponsableId = actual != null ? actual.UsuarioResponsableId : null,
                SiguienteAccion = metadata.SiguienteAccion,
                Observacion = observacion,
                FechaEstado = DateTime.Now,
                Activo = true
            };

            _procesoEstadoDao.UpsertEstadoActual(record);

            // Regla anti-duplicado: No insertar dos veces el mismo estado, accion y solicitud en menos de 10 segundos para el mismo usuario
            var recentHistory = _procesoEstadoDao.ObtenerHistorialPorSolicitud(solicitudId);
            bool isDuplicate = false;
            if (recentHistory != null && recentHistory.Count > 0)
            {
                var last = recentHistory[recentHistory.Count - 1];
                if (last.EstadoNuevo == estadoDestino &&
                    last.Accion == accion &&
                    last.UsuarioId == (usuarioId > 0 ? (int?)usuarioId : null) &&
                    Math.Abs((DateTime.Now - last.FechaCreacion).TotalSeconds) < 10)
                {
                    isDuplicate = true;
                }
            }

            if (!isDuplicate)
            {
                _procesoEstadoDao.InsertarHistorial(new AocrProcesoEstadoHistorialRecord
                {
                    SolicitudId = solicitudId,
                    OrdenRecaudacionId = record.OrdenRecaudacionId,
                    InspeccionId = record.InspeccionId,
                    InformeId = record.InformeId,
                    EstadoAnterior = estadoAnterior,
                    EstadoNuevo = estadoDestino,
                    Etapa = record.EtapaActual,
                    Accion = accion,
                    RolUsuario = NormalizeRole(rolUsuario),
                    UsuarioId = usuarioId > 0 ? (int?)usuarioId : null,
                    RolResponsable = record.RolResponsable,
                    UsuarioResponsableId = record.UsuarioResponsableId,
                    Observacion = observacion,
                    FechaCreacion = DateTime.Now
                });
            }

            Trace.TraceInformation("[AOCR_ESTADO][CAMBIO_OK] SolicitudId=" + solicitudId + "; EstadoNuevo=" + estadoDestino + ";");
            SafeProcessNotifications(solicitudId, estadoDestino);
            return new AocrEstadoProcesoResult
            {
                Ok = true,
                EstadoAnterior = estadoAnterior,
                EstadoNuevo = estadoDestino,
                EstadoActual = record
            };
        }

        public AocrEstadoProcesoResult SincronizarDesdeFuentesActuales(
            int solicitudId,
            string accion,
            int usuarioId,
            string rolUsuario,
            string observacion = null,
            int? ordenRecaudacionId = null,
            int? inspeccionId = null,
            int? informeId = null)
        {
            if (solicitudId <= 0)
            {
                return new AocrEstadoProcesoResult { Ok = false, Motivo = "Solicitud invalida." };
            }

            var inferred = InferState(solicitudId, ordenRecaudacionId, inspeccionId, informeId);
            if (string.IsNullOrWhiteSpace(inferred.State))
            {
                return new AocrEstadoProcesoResult { Ok = false, Motivo = "No se pudo inferir el estado." };
            }

            Trace.TraceInformation(
                "[AOCR_ESTADO][MIGRACION_INICIAL] SolicitudId=" + solicitudId +
                "; EstadoDetectado=" + (inferred.DetectedSourceState ?? string.Empty) +
                "; EstadoCentral=" + inferred.State + ";");

            return CambiarEstado(
                solicitudId,
                inferred.State,
                accion,
                usuarioId,
                rolUsuario,
                string.IsNullOrWhiteSpace(observacion) ? inferred.Observation : observacion,
                ordenRecaudacionId ?? inferred.OrderId,
                inspeccionId ?? inferred.InspectionId,
                informeId ?? inferred.ReportId);
        }

        public bool PuedeEjecutarAccion(int solicitudId, string accion, string rolUsuario)
        {
            var actual = ObtenerEstadoActual(solicitudId);
            var role = NormalizeRole(rolUsuario);
            var allowed = actual != null && ObtenerAccionesPermitidas(actual.EstadoActual, role).Contains((accion ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);
            Trace.TraceInformation(
                "[AOCR_ESTADO][ACCION_VALIDAR] SolicitudId=" + solicitudId +
                "; Accion=" + (accion ?? string.Empty) +
                "; Rol=" + role +
                "; Permitida=" + allowed + ";");
            return allowed;
        }

        public string ObtenerSiguientePaso(int solicitudId, string rolUsuario)
        {
            var actual = ObtenerEstadoActual(solicitudId);
            if (actual == null)
            {
                return string.Empty;
            }

            var role = NormalizeRole(rolUsuario);
            if (!string.IsNullOrWhiteSpace(role) &&
                !string.Equals(actual.RolResponsable, role, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return actual.SiguienteAccion ?? string.Empty;
        }

        public IReadOnlyList<string> ObtenerAccionesPermitidas(int solicitudId, string rolUsuario)
        {
            var actual = ObtenerEstadoActual(solicitudId);
            return actual == null ? new string[0] : ObtenerAccionesPermitidas(actual.EstadoActual, NormalizeRole(rolUsuario));
        }

        private IReadOnlyList<string> ObtenerAccionesPermitidas(string estadoActual, string rolUsuario)
        {
            if (string.IsNullOrWhiteSpace(estadoActual) || string.IsNullOrWhiteSpace(rolUsuario))
            {
                return new string[0];
            }

            string[] actions;
            if (AllowedActionsByRoleAndState.TryGetValue(Key(estadoActual, rolUsuario), out actions))
            {
                return actions;
            }

            return string.Equals(rolUsuario, "Administrador", StringComparison.OrdinalIgnoreCase)
                ? new[] { "VER_DETALLE", "SINCRONIZAR_ESTADO" }
                : new string[0];
        }

        private InferredState InferState(int solicitudId, int? orderId, int? inspectionId, int? reportId)
        {
            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            var inspection = ResolveInspection(solicitudId, inspectionId);
            var report = ResolveReport(inspection, reportId);
            var lv = inspection != null && inspection.CodigoInspeccion > 0
                ? _lvDao.ObtenerUltimaPorInspeccion(inspection.CodigoInspeccion)
                : null;

            var finalAocr = _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
            var finalCondiciones = _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES_LIMITACIONES")
                ?? _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES");
            var aocrGenerado = _documentoGeneradoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "AOCR_GENERADO");
            var condicionesGeneradas = _documentoGeneradoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES_LIMITACIONES")
                ?? _documentoGeneradoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES");

            if (finalCondiciones != null)
            {
                return Result(AocrEstadosProceso.CondicionesFirmadas, "Firma documento CONDICIONES", orderId, inspection, report, "Documento de condiciones firmado.");
            }

            if (condicionesGeneradas != null)
            {
                return Result(AocrEstadosProceso.CondicionesPdfGenerado, "Documento generado CONDICIONES", orderId, inspection, report, "Documento de condiciones generado.");
            }

            if (finalAocr != null)
            {
                return Result(AocrEstadosProceso.AocrFirmado, "Firma documento AOCR", orderId, inspection, report, "Documento AOCR firmado.");
            }

            if (aocrGenerado != null)
            {
                return Result(AocrEstadosProceso.AocrPdfGenerado, "Documento generado AOCR", orderId, inspection, report, "Documento AOCR generado.");
            }

            if (report != null)
            {
                var estadoInforme = NormalizeReportState(report.EstadoInforme, report);
                if (!string.IsNullOrWhiteSpace(estadoInforme))
                {
                    return Result(estadoInforme, report.EstadoInforme, orderId, inspection, report, "Estado inferido desde informe tecnico.");
                }
            }

            if (lv != null)
            {
                var estadoLv = NormalizeLvState(lv);
                if (!string.IsNullOrWhiteSpace(estadoLv))
                {
                    return Result(estadoLv, lv.EstadoLista, orderId, inspection, report, "Estado inferido desde LV/EAE.");
                }
            }

            if (inspection != null)
            {
                var estadoInspeccion = NormalizeInspectionState(inspection);
                if (!string.IsNullOrWhiteSpace(estadoInspeccion))
                {
                    return Result(estadoInspeccion, inspection.EstadoDocumental ?? inspection.Estado, orderId, inspection, report, "Estado inferido desde inspeccion.");
                }
            }

            var estadoSolicitud = solicitud != null ? NormalizeSolicitudState(solicitud.Estado) : null;
            if (!string.IsNullOrWhiteSpace(estadoSolicitud))
            {
                return Result(estadoSolicitud, solicitud.Estado, orderId, inspection, report, "Estado inferido desde solicitud.");
            }

            return Result(AocrEstadosProceso.OrdenRequerida, "SIN_ESTADO", orderId, inspection, report, "Estado base sin informacion adicional.");
        }

        private static bool CanTransition(string estadoActual, string estadoDestino)
        {
            if (string.IsNullOrWhiteSpace(estadoDestino))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(estadoActual))
            {
                return true;
            }

            if (string.Equals(estadoActual, estadoDestino, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] allowed;
            return AllowedTransitions.TryGetValue(estadoActual, out allowed)
                && allowed.Any(item => string.Equals(item, estadoDestino, StringComparison.OrdinalIgnoreCase));
        }

        private static bool StrictValidationEnabled()
        {
            var raw = ConfigurationManager.AppSettings["toggle.aocr.estadoProceso.strict"];
            bool enabled;
            return bool.TryParse(raw, out enabled) && enabled;
        }

        private static AocrEstadoProcesoResult Deny(int solicitudId, string estadoActual, string estadoIntentado, string motivo)
        {
            Trace.TraceWarning(
                "[AOCR_ESTADO][CAMBIO_DENEGADO] SolicitudId=" + solicitudId +
                "; EstadoActual=" + (estadoActual ?? string.Empty) +
                "; EstadoIntentado=" + (estadoIntentado ?? string.Empty) +
                "; Motivo=" + (motivo ?? string.Empty) + ";");

            return new AocrEstadoProcesoResult
            {
                Ok = false,
                EstadoAnterior = estadoActual,
                EstadoNuevo = estadoIntentado,
                Motivo = motivo
            };
        }

        private void SafeProcessNotifications(int solicitudId, string estadoNuevo)
        {
            try
            {
                ProcesarNotificacionPorEstado(solicitudId, estadoNuevo);
                Trace.TraceInformation("[AOCR_ESTADO][NOTIFICACION_DISPARADA] SolicitudId=" + solicitudId + "; EstadoNuevo=" + estadoNuevo + ";");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[AOCR_ESTADO][NOTIFICACION_ERROR] SolicitudId=" + solicitudId + "; EstadoNuevo=" + estadoNuevo + "; Error=" + ex.Message + ";");
            }
        }

        private void ProcesarNotificacionPorEstado(int solicitudId, string estadoNuevo)
        {
            switch (NormalizeProcesoState(estadoNuevo))
            {
                case AocrEstadosProceso.AocrFirmado:
                    new AocrProcesoNotificacionService().NotificarAocrFirmado(solicitudId);
                    break;
                case AocrEstadosProceso.CondicionesFirmadas:
                    new AocrProcesoNotificacionService().NotificarCondicionesFirmadas(solicitudId);
                    break;
                case AocrEstadosProceso.DocumentosFinalesLiberadosRt:
                case AocrEstadosProceso.AocrFinalizado:
                    new AocrProcesoNotificacionService().NotificarProcesoAocrFinalizado(solicitudId);
                    break;
            }
        }

        private Inspeccion ResolveInspection(int solicitudId, int? inspectionId)
        {
            if (inspectionId.HasValue && inspectionId.Value > 0)
            {
                return _inspeccionDao.ObtenerPorId(inspectionId.Value);
            }

            var inspections = _inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>();
            return inspections
                .Where(item => item != null)
                .OrderByDescending(item => item.CodigoInspeccion)
                .FirstOrDefault();
        }

        private InspeccionInformeTecnico ResolveReport(Inspeccion inspection, int? reportId)
        {
            if (reportId.HasValue && reportId.Value > 0)
            {
                return _informeDao.ObtenerPorId(reportId.Value);
            }

            return inspection != null && inspection.CodigoInspeccion > 0
                ? _informeDao.ObtenerUltimoPorInspeccion(inspection.CodigoInspeccion)
                : null;
        }

        private string NormalizeSolicitudState(string estadoSolicitud)
        {
            if (string.IsNullOrWhiteSpace(estadoSolicitud))
            {
                return null;
            }

            var institucional = _estadoService.NormalizarClaveInstitucional(estadoSolicitud);
            switch (institucional)
            {
                case "ORDEN_GENERADA":
                    return AocrEstadosProceso.OrdenGenerada;
                case "PAGO_PENDIENTE":
                    return AocrEstadosProceso.PagoRegistrado;
                case "PAGO_APROBADO":
                    return AocrEstadosProceso.PagoAprobado;
                case "SOLICITUD_AOCR_HABILITADA":
                    return AocrEstadosProceso.SolicitudAocrHabilitada;
                case "DOCUMENTACION_EN_CARGA":
                    return AocrEstadosProceso.SolicitudAocrEnBorrador;
                case "DOCUMENTACION_ENVIADA":
                case "EN_REVISION_COORDINADOR":
                    return AocrEstadosProceso.SolicitudAocrEnviada;
                case "DEVUELTO_RT_OBSERVACIONES":
                    return AocrEstadosProceso.SubsanacionRequerida;
                case "SUBSANADA":
                    return AocrEstadosProceso.SubsanacionEnviada;
                case "DOCUMENTACION_ACEPTADA_COORDINADOR":
                    return AocrEstadosProceso.DocumentacionAceptada;
                case "PENDIENTE_ASIGNACION_INSPECTOR":
                    return AocrEstadosProceso.PendienteAsignacionInspector;
                case "INSPECTOR_ASIGNADO":
                    return AocrEstadosProceso.InspectorAsignado;
                case "EN_REVISION_TECNICA":
                    return AocrEstadosProceso.RevisionDocumental;
                case "LV_EN_PROCESO":
                    return AocrEstadosProceso.LvEnProceso;
                case "LV_FINALIZADA":
                    return AocrEstadosProceso.LvFinalizada;
                case "LV_FIRMADA":
                    return AocrEstadosProceso.LvFirmada;
                case "INFORME_TECNICO_FIRMADO":
                    return AocrEstadosProceso.InformeTecnicoFirmado;
                case "INFORME_TECNICO_FIRMADO_INSPECTOR":
                    return AocrEstadosProceso.InformeTecnicoFirmadoInspector;
                case "AOCR_EN_ELABORACION":
                    return AocrEstadosProceso.AocrDatosPendientes;
                case "AOCR_EN_REVISION_COORDINADOR":
                case "AOCR_ENVIADO_DIRDAC":
                    return AocrEstadosProceso.InformeEnviadoDireccion;
                case "AOCR_FIRMADO":
                    return AocrEstadosProceso.AocrFirmado;
                case "CONDICIONES_FIRMADAS":
                    return AocrEstadosProceso.CondicionesFirmadas;
                case "DOCUMENTOS_FINALES_DISPONIBLES":
                    return AocrEstadosProceso.DocumentosFinalesLiberadosRt;
                case "ANULADO":
                    return AocrEstadosProceso.AocrAnulado;
                case "CERRADO":
                    return AocrEstadosProceso.AocrFinalizado;
            }

            var canonico = _estadoService.Normalizar(estadoSolicitud);
            if (string.Equals(canonico, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.DocumentacionObservada;
            }

            if (string.Equals(canonico, EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.SubsanacionEnviada;
            }

            if (string.Equals(canonico, EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.DocumentacionAceptada;
            }

            if (string.Equals(canonico, EstadoSolicitud.PendienteAsignacionRT, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.PendienteAsignacionInspector;
            }

            if (string.Equals(canonico, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.RevisionDocumental;
            }

            if (string.Equals(canonico, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.AocrDatosPendientes;
            }

            if (string.Equals(canonico, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.InformeEnviadoDireccion;
            }

            if (string.Equals(canonico, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.AocrDatosCompletos;
            }

            if (string.Equals(canonico, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(canonico, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.DocumentosFinalesLiberadosRt;
            }

            if (string.Equals(canonico, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.AocrFinalizado;
            }

            if (string.Equals(canonico, EstadoSolicitud.Anulada, StringComparison.OrdinalIgnoreCase))
            {
                return AocrEstadosProceso.AocrAnulado;
            }

            return null;
        }

        private static string NormalizeInspectionState(Inspeccion inspection)
        {
            if (inspection == null)
            {
                return null;
            }

            var estadoDocumental = (inspection.EstadoDocumental ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
            switch (estadoDocumental)
            {
                case "EN_REVISION":
                case "PENDIENTE_REVISION":
                    return AocrEstadosProceso.RevisionDocumental;
                case "ACEPTADA":
                case "APROBADO":
                    return AocrEstadosProceso.DocumentacionAceptada;
                case "OBSERVADA":
                case "DEVUELTA":
                case "RECHAZADA":
                    return AocrEstadosProceso.DocumentacionObservada;
            }

            var estado = (inspection.Estado ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
            switch (estado)
            {
                case "SOLICITUD_INSPECCION_CREADA":
                case "SOLICITUD_INSPECCION_GENERADA":
                    return AocrEstadosProceso.SolicitudInspeccionGenerada;
                case "SOLICITUD_INSPECCION_FIRMADA":
                case "SOLICITUD_INSPECCION_CARGADA":
                    return AocrEstadosProceso.SolicitudInspeccionCargada;
                case "EN_INSPECCION":
                case "EN_REVISION":
                    return AocrEstadosProceso.RevisionDocumental;
                case "SUBSANADA":
                    return AocrEstadosProceso.SubsanacionEnviada;
                case "INFORME_ELABORADO":
                    return AocrEstadosProceso.InformeTecnicoGenerado;
            }

            return inspection.CodigoInspector.HasValue && inspection.CodigoInspector.Value > 0
                ? AocrEstadosProceso.InspectorAsignado
                : null;
        }

        private static string NormalizeLvState(ListaVerificacionOperacionalEae lv)
        {
            if (lv == null)
            {
                return null;
            }

            if (lv.FirmadoTecnico)
            {
                return AocrEstadosProceso.LvFirmada;
            }

            if (lv.Finalizado)
            {
                return AocrEstadosProceso.LvFinalizada;
            }

            var estado = (lv.EstadoLista ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
            if (estado == "BORRADOR")
            {
                return AocrEstadosProceso.LvPendiente;
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                return AocrEstadosProceso.LvEnProceso;
            }

            return null;
        }

        private static string NormalizeReportState(string estadoInforme, InspeccionInformeTecnico report)
        {
            if (report == null)
            {
                return null;
            }

            if (report.FirmadoDirdac)
            {
                return AocrEstadosProceso.InformeAprobadoDireccion;
            }

            if (report.FechaFirma2.HasValue && !string.IsNullOrWhiteSpace(report.UsuarioFirma2))
            {
                return AocrEstadosProceso.InformeAprobadoDireccion;
            }

            if (report.FirmadoInspector && report.Finalizado)
            {
                var raw = (estadoInforme ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
                switch (raw)
                {
                    case "INFORME_TECNICO_APROBADO_DCAV":
                        return AocrEstadosProceso.InformeTecnicoAprobadoDcav;
                    case "INFORME_TECNICO_OBSERVADO_DCAV":
                        return AocrEstadosProceso.InformeTecnicoObservadoDcav;
                    case "PENDIENTE_REVISION_INFORME_DCAV":
                        return AocrEstadosProceso.PendienteRevisionInformeDcav;
                    case "APROBADO_DIRECCION":
                    case "APROBADO_DIRDAC":
                    case "FIRMADO_FINAL":
                        return AocrEstadosProceso.InformeAprobadoDireccion;
                    case "OBSERVADO_DIRDAC":
                    case "DEVUELTO_DIRECCION":
                    case "RECHAZADO_DIRDAC":
                        return AocrEstadosProceso.InformeDevueltoDireccion;
                    case "FIRMADO_INSPECTOR":
                    case "INFORME_TECNICO_FIRMADO_INSPECTOR":
                        return AocrEstadosProceso.InformeTecnicoFirmadoInspector;
                    case "FIRMADO":
                        return AocrEstadosProceso.InformeTecnicoFirmado;
                    default:
                        return AocrEstadosProceso.InformeTecnicoFirmado;
                }
            }

            if (report.Finalizado)
            {
                return AocrEstadosProceso.InformeTecnicoGenerado;
            }

            if (!string.IsNullOrWhiteSpace(estadoInforme))
            {
                return AocrEstadosProceso.InformeTecnicoPendiente;
            }

            return null;
        }

        private static string NormalizeProcesoState(string state)
        {
            return (state ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeRole(string role)
        {
            var normalized = Simplify(role);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (Matches(normalized, "ADMIN", "ADMINISTRADOR")) return "Administrador";
            if (Matches(normalized, "SOLICITANTE", "OPERADOR", "REPRESENTANTETECNICO", "REPRESENTANTELEGAL", "RT")) return "Solicitante";
            if (Matches(normalized, "INSPECTOR", "TECNICO", "EVALUADORTECNICO", "INSPECTORTECNICO")) return "InspectorTecnico";
            if (Matches(normalized, "FINANCIERO", "COORDINADORFINANCIERO", "DIRECTORFINANCIERO")) return "Financiero";
            if (Matches(normalized, "COORDINACION", "COORDINADOR", "COORDINADORINSPECCIONES", "COORDINACIONLEGAL", "COORDINADORLEGAL")) return "Coordinacion";
            if (Matches(normalized, "DIRECTORGENERAL", "DIRECTOR_GENERAL", "DIRDAC")) return "DirectorGeneral";
            if (Matches(normalized, "DIRECTORCERTIFICACIONESDCAV", "DIRECTORDECERTIFICACIONESDCAV", "DIRECTOR_CERTIFICACIONES_DCAV", "DIRECCIONCERTIFICACION", "DIRECCIONCERTIFICACIONES", "DCAV")) return "DirectorCertificacionesDcav";
            if (Matches(normalized, "DIRECCION", "JEFATURATECNICA", "DIRECCIONJEFATURA", "DIRECCIONJEFATURATECNICA")) return "DireccionJefaturaTecnica";
            return (role ?? string.Empty).Trim();
        }

        private static EstadoMetadata ResolveMetadata(string state)
        {
            EstadoMetadata metadata;
            return MetadataByState.TryGetValue(state ?? string.Empty, out metadata)
                ? metadata
                : Meta("GENERAL", "Administrador", "Sin siguiente accion", "SIN_ACCION");
        }

        private static EstadoMetadata Meta(string etapa, string rolResponsable, string siguienteAccion, string accionClave)
        {
            return new EstadoMetadata
            {
                Etapa = etapa,
                RolResponsable = rolResponsable,
                SiguienteAccion = siguienteAccion,
                AccionClave = accionClave
            };
        }

        private static string Key(string state, string role)
        {
            return NormalizeProcesoState(state) + "|" + (role ?? string.Empty).Trim();
        }

        private static InferredState Result(string state, string detectedSourceState, int? orderId, Inspeccion inspection, InspeccionInformeTecnico report, string observation)
        {
            return new InferredState
            {
                State = state,
                DetectedSourceState = detectedSourceState,
                OrderId = orderId,
                InspectionId = inspection != null ? (int?)inspection.CodigoInspeccion : null,
                ReportId = report != null ? (int?)report.CodigoInforme : null,
                Observation = observation
            };
        }

        private sealed class InferredState
        {
            public string State { get; set; }
            public string DetectedSourceState { get; set; }
            public int? OrderId { get; set; }
            public int? InspectionId { get; set; }
            public int? ReportId { get; set; }
            public string Observation { get; set; }
        }

        private sealed class EstadoMetadata
        {
            public string Etapa { get; set; }
            public string RolResponsable { get; set; }
            public string SiguienteAccion { get; set; }
            public string AccionClave { get; set; }
        }

        private static bool Matches(string normalizedRole, params string[] aliases)
        {
            return aliases.Any(alias => normalizedRole.Equals(alias, StringComparison.OrdinalIgnoreCase));
        }

        private static string Simplify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().ToUpperInvariant()
                .Replace('Á', 'A')
                .Replace('É', 'E')
                .Replace('Í', 'I')
                .Replace('Ó', 'O')
                .Replace('Ú', 'U')
                .Replace('Ü', 'U')
                .Replace('Ñ', 'N');

            var chars = normalized.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars);
        }
    }
}
