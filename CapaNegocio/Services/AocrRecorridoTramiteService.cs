using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class AocrRecorridoTramiteService
    {
        private readonly AocrProcesoEstadoDAO _procesoEstadoDao;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly AocrEstadoProcesoService _estadoProcesoService;

        private static readonly string[] StandardStateSequence = new[]
        {
            AocrEstadosProceso.OrdenRequerida,
            AocrEstadosProceso.OrdenGenerada,
            AocrEstadosProceso.PagoRegistrado,
            AocrEstadosProceso.PagoAprobado,
            AocrEstadosProceso.Fr3Vinculado,
            AocrEstadosProceso.SolicitudAocrHabilitada,
            AocrEstadosProceso.SolicitudAocrEnBorrador,
            AocrEstadosProceso.SolicitudAocrEnviada,
            AocrEstadosProceso.InspectorAsignado,
            AocrEstadosProceso.RevisionDocumental,
            AocrEstadosProceso.DocumentacionAceptada,
            AocrEstadosProceso.LvPendiente,
            AocrEstadosProceso.LvEnProceso,
            AocrEstadosProceso.LvFinalizada,
            AocrEstadosProceso.LvFirmada,
            AocrEstadosProceso.InformeTecnicoPendiente,
            AocrEstadosProceso.InformeTecnicoGenerado,
            AocrEstadosProceso.InformeTecnicoFirmadoInspector,
            AocrEstadosProceso.PendienteRevisionInformeDcav,
            AocrEstadosProceso.InformeTecnicoAprobadoDcav,
            AocrEstadosProceso.DocumentosHabilitadosInspector,
            AocrEstadosProceso.DocumentosEnRevisionInspector,
            AocrEstadosProceso.PendienteRevisionDocumentosDcav,
            AocrEstadosProceso.AprobadoDocumentosDcav,
            AocrEstadosProceso.PendienteFirmaDirectorGeneral,
            AocrEstadosProceso.DocumentosFirmadosDirdac,
            AocrEstadosProceso.DocumentosFinalesLiberadosRt,
            AocrEstadosProceso.AocrFinalizado
        };

        private sealed class StateMeta
        {
            public string Etapa { get; set; }
            public string RolResponsable { get; set; }
            public string SiguienteAccion { get; set; }
        }

        private static readonly Dictionary<string, StateMeta> MetadataMap = new Dictionary<string, StateMeta>(StringComparer.OrdinalIgnoreCase)
        {
            { AocrEstadosProceso.OrdenRequerida, new StateMeta { Etapa = "RECAUDACION", RolResponsable = "Solicitante", SiguienteAccion = "Crear orden" } },
            { AocrEstadosProceso.OrdenGenerada, new StateMeta { Etapa = "RECAUDACION", RolResponsable = "Solicitante", SiguienteAccion = "Registrar pago" } },
            { AocrEstadosProceso.PagoRegistrado, new StateMeta { Etapa = "PAGO", RolResponsable = "Financiero", SiguienteAccion = "Revisar comprobante" } },
            { AocrEstadosProceso.PagoEnRevision, new StateMeta { Etapa = "PAGO", RolResponsable = "Financiero", SiguienteAccion = "Aprobar o rechazar pago" } },
            { AocrEstadosProceso.PagoAprobado, new StateMeta { Etapa = "PAGO", RolResponsable = "Financiero", SiguienteAccion = "Vincular FR3" } },
            { AocrEstadosProceso.PagoRechazado, new StateMeta { Etapa = "PAGO", RolResponsable = "Solicitante", SiguienteAccion = "Subir nuevo comprobante" } },
            { AocrEstadosProceso.Fr3Pendiente, new StateMeta { Etapa = "FR3", RolResponsable = "Financiero", SiguienteAccion = "Generar o sincronizar FR3" } },
            { AocrEstadosProceso.Fr3Vinculado, new StateMeta { Etapa = "FR3", RolResponsable = "Solicitante", SiguienteAccion = "Continuar solicitud AOCR" } },
            { AocrEstadosProceso.SolicitudAocrHabilitada, new StateMeta { Etapa = "SOLICITUD_AOCR", RolResponsable = "Solicitante", SiguienteAccion = "Completar solicitud AOCR" } },
            { AocrEstadosProceso.SolicitudAocrEnBorrador, new StateMeta { Etapa = "SOLICITUD_AOCR", RolResponsable = "Solicitante", SiguienteAccion = "Enviar solicitud AOCR" } },
            { AocrEstadosProceso.SolicitudAocrEnviada, new StateMeta { Etapa = "COORDINACION", RolResponsable = "Coordinacion", SiguienteAccion = "Revisar solicitud" } },
            { AocrEstadosProceso.PendienteAsignacionInspector, new StateMeta { Etapa = "COORDINACION", RolResponsable = "Coordinacion", SiguienteAccion = "Asignar inspector" } },
            { AocrEstadosProceso.InspectorAsignado, new StateMeta { Etapa = "INSPECCION", RolResponsable = "InspectorTecnico", SiguienteAccion = "Abrir revision documental" } },
            { AocrEstadosProceso.RevisionDocumental, new StateMeta { Etapa = "REVISION_DOCUMENTAL", RolResponsable = "InspectorTecnico", SiguienteAccion = "Revisar documentacion" } },
            { AocrEstadosProceso.DocumentacionObservada, new StateMeta { Etapa = "REVISION_DOCUMENTAL", RolResponsable = "Solicitante", SiguienteAccion = "Atender observaciones" } },
            { AocrEstadosProceso.SubsanacionRequerida, new StateMeta { Etapa = "REVISION_DOCUMENTAL", RolResponsable = "Solicitante", SiguienteAccion = "Cargar subsanacion" } },
            { AocrEstadosProceso.SubsanacionEnviada, new StateMeta { Etapa = "REVISION_DOCUMENTAL", RolResponsable = "InspectorTecnico", SiguienteAccion = "Revisar subsanacion" } },
            { AocrEstadosProceso.DocumentacionAceptada, new StateMeta { Etapa = "LV_EAE", RolResponsable = "InspectorTecnico", SiguienteAccion = "Iniciar LV/EAE" } },
            { AocrEstadosProceso.LvPendiente, new StateMeta { Etapa = "LV_EAE", RolResponsable = "InspectorTecnico", SiguienteAccion = "Iniciar LV/EAE" } },
            { AocrEstadosProceso.LvEnProceso, new StateMeta { Etapa = "LV_EAE", RolResponsable = "InspectorTecnico", SiguienteAccion = "Finalizar LV/EAE" } },
            { AocrEstadosProceso.LvFinalizada, new StateMeta { Etapa = "LV_EAE", RolResponsable = "InspectorTecnico", SiguienteAccion = "Firmar LV/EAE" } },
            { AocrEstadosProceso.LvFirmada, new StateMeta { Etapa = "INFORME_TECNICO", RolResponsable = "InspectorTecnico", SiguienteAccion = "Generar informe tecnico" } },
            { AocrEstadosProceso.InformeTecnicoPendiente, new StateMeta { Etapa = "INFORME_TECNICO", RolResponsable = "InspectorTecnico", SiguienteAccion = "Generar informe tecnico" } },
            { AocrEstadosProceso.InformeTecnicoGenerado, new StateMeta { Etapa = "INFORME_TECNICO", RolResponsable = "InspectorTecnico", SiguienteAccion = "Firmar informe tecnico" } },
            { AocrEstadosProceso.InformeTecnicoFirmado, new StateMeta { Etapa = "INFORME_TECNICO", RolResponsable = "DireccionJefaturaTecnica", SiguienteAccion = "Revisar informe tecnico" } },
            { AocrEstadosProceso.InformeTecnicoFirmadoInspector, new StateMeta { Etapa = "REVISION_INFORME_DCAV", RolResponsable = "DirectorCertificacionesDcav", SiguienteAccion = "Revisar informe tecnico firmado" } },
            { AocrEstadosProceso.PendienteRevisionInformeDcav, new StateMeta { Etapa = "REVISION_INFORME_DCAV", RolResponsable = "DirectorCertificacionesDcav", SiguienteAccion = "Aprobar u observar informe tecnico" } },
            { AocrEstadosProceso.InformeTecnicoObservadoDcav, new StateMeta { Etapa = "INFORME_TECNICO", RolResponsable = "InspectorTecnico", SiguienteAccion = "Corregir y volver a firmar informe tecnico" } },
            { AocrEstadosProceso.InformeTecnicoAprobadoDcav, new StateMeta { Etapa = "DOCUMENTOS_AOCR", RolResponsable = "InspectorTecnico", SiguienteAccion = "Generar AOCR y Condiciones y Limitaciones" } },
            { AocrEstadosProceso.DocumentosHabilitadosInspector, new StateMeta { Etapa = "DOCUMENTOS_AOCR", RolResponsable = "InspectorTecnico", SiguienteAccion = "Generar AOCR y Condiciones y Limitaciones" } },
            { AocrEstadosProceso.DocumentosEnRevisionInspector, new StateMeta { Etapa = "DOCUMENTOS_AOCR", RolResponsable = "InspectorTecnico", SiguienteAccion = "Completar y enviar documentos a DCAV" } },
            { AocrEstadosProceso.PendienteRevisionDocumentosDcav, new StateMeta { Etapa = "REVISION_DOCUMENTOS_DCAV", RolResponsable = "DirectorCertificacionesDcav", SiguienteAccion = "Aprobar u observar documentos AOCR" } },
            { AocrEstadosProceso.DocumentosObservadosDcav, new StateMeta { Etapa = "DOCUMENTOS_AOCR", RolResponsable = "InspectorTecnico", SiguienteAccion = "Corregir documentos AOCR" } },
            { AocrEstadosProceso.AprobadoDocumentosDcav, new StateMeta { Etapa = "FIRMA_DIRDAC", RolResponsable = "DirectorGeneral", SiguienteAccion = "Firmar AOCR y Condiciones y Limitaciones" } },
            { AocrEstadosProceso.PendienteFirmaDirectorGeneral, new StateMeta { Etapa = "FIRMA_DIRDAC", RolResponsable = "DirectorGeneral", SiguienteAccion = "Firmar AOCR y Condiciones y Limitaciones" } },
            { AocrEstadosProceso.PendienteFirmaDirectorGeneralLegacy, new StateMeta { Etapa = "FIRMA_DIRDAC", RolResponsable = "DirectorGeneral", SiguienteAccion = "Firmar AOCR y Condiciones y Limitaciones" } },
            { AocrEstadosProceso.AocrFirmadoDirdac, new StateMeta { Etapa = "FIRMA_DIRDAC", RolResponsable = "DirectorGeneral", SiguienteAccion = "Firmar Condiciones y Limitaciones" } },
            { AocrEstadosProceso.CondicionesFirmadasDirdac, new StateMeta { Etapa = "FIRMA_DIRDAC", RolResponsable = "DirectorGeneral", SiguienteAccion = "Firmar AOCR" } },
            { AocrEstadosProceso.DocumentosFirmadosDirdac, new StateMeta { Etapa = "BANDEJA_FINAL", RolResponsable = "Solicitante", SiguienteAccion = "Descargar documentos finales" } },
            { AocrEstadosProceso.InformeEnviadoDireccion, new StateMeta { Etapa = "DIRECCION", RolResponsable = "DireccionJefaturaTecnica", SiguienteAccion = "Aprobar o devolver informe tecnico" } },
            { AocrEstadosProceso.InformeAprobadoDireccion, new StateMeta { Etapa = "DIRECCION", RolResponsable = "DireccionJefaturaTecnica", SiguienteAccion = "Completar datos AOCR" } },
            { AocrEstadosProceso.InformeDevueltoDireccion, new StateMeta { Etapa = "DIRECCION", RolResponsable = "InspectorTecnico", SiguienteAccion = "Ajustar informe tecnico" } },
            { AocrEstadosProceso.AocrDatosPendientes, new StateMeta { Etapa = "FIRMA_AOCR", RolResponsable = "DireccionJefaturaTecnica", SiguienteAccion = "Completar datos AOCR" } },
            { AocrEstadosProceso.AocrDatosCompletos, new StateMeta { Etapa = "FIRMA_AOCR", RolResponsable = "DireccionJefaturaTecnica", SiguienteAccion = "Generar PDF AOCR" } },
            { AocrEstadosProceso.AocrPdfGenerado, new StateMeta { Etapa = "FIRMA_AOCR", RolResponsable = "DireccionJefaturaTecnica", SiguienteAccion = "Firmar AOCR" } },
            { AocrEstadosProceso.AocrFirmado, new StateMeta { Etapa = "CONDICIONES", RolResponsable = "DireccionJefaturaTecnica", SiguienteAccion = "Generar condiciones y limitaciones" } },
            { AocrEstadosProceso.CondicionesPdfGenerado, new StateMeta { Etapa = "CONDICIONES", RolResponsable = "DireccionJefaturaTecnica", SiguienteAccion = "Firmar condiciones y limitaciones" } },
            { AocrEstadosProceso.CondicionesFirmadas, new StateMeta { Etapa = "BANDEJA_FINAL", RolResponsable = "DireccionJefaturaTecnica", SiguienteAccion = "Liberar documentos finales al RT" } },
            { AocrEstadosProceso.DocumentosFinalesLiberadosRt, new StateMeta { Etapa = "BANDEJA_FINAL", RolResponsable = "Solicitante", SiguienteAccion = "Descargar documentos finales" } },
            { AocrEstadosProceso.AocrFinalizado, new StateMeta { Etapa = "CIERRE", RolResponsable = "Solicitante", SiguienteAccion = "Proceso finalizado" } },
            { AocrEstadosProceso.AocrAnulado, new StateMeta { Etapa = "CIERRE", RolResponsable = "Administrador", SiguienteAccion = "Proceso anulado" } }
        };

        public AocrRecorridoTramiteService()
        {
            _procesoEstadoDao = new AocrProcesoEstadoDAO();
            _solicitudDao = new SolicitudAOCRDAO();
            _estadoProcesoService = new AocrEstadoProcesoService();
        }

        public AocrResumenEstadoActualViewModel ObtenerResumenEstadoActual(int solicitudId)
        {
            if (solicitudId <= 0) return null;

            Trace.TraceInformation($"[RECORRIDO][ESTADO_ACTUAL_IN] SolicitudId={solicitudId}");
            var actual = _estadoProcesoService.ObtenerEstadoActual(solicitudId);
            if (actual == null)
            {
                Trace.TraceWarning($"[RECORRIDO][ESTADO_ACTUAL_EMPTY] SolicitudId={solicitudId}");
                return null;
            }

            var meta = ResolveMeta(actual.EstadoActual);
            var vm = new AocrResumenEstadoActualViewModel
            {
                SolicitudId = solicitudId,
                EstadoActual = FriendlyStateName(actual.EstadoActual),
                EtapaActual = meta.Etapa,
                RolResponsable = actual.RolResponsable ?? meta.RolResponsable,
                Responsable = "Pendiente",
                SiguienteAccion = actual.SiguienteAccion ?? meta.SiguienteAccion,
                FechaEstado = actual.FechaEstado.ToString("dd/MM/yyyy HH:mm")
            };

            if (actual.UsuarioResponsableId.HasValue && actual.UsuarioResponsableId.Value > 0)
            {
                vm.Responsable = "Usuario ID: " + actual.UsuarioResponsableId.Value;
            }

            Trace.TraceInformation($"[RECORRIDO][ESTADO_ACTUAL] SolicitudId={solicitudId}; EstadoActual={vm.EstadoActual}; SiguienteAccion={vm.SiguienteAccion}");
            return vm;
        }

        public List<AocrRecorridoEstadoViewModel> ObtenerRecorrido(int solicitudId, string rolActivo, int usuarioId, string companiaActiva)
        {
            Trace.TraceInformation($"[RECORRIDO][LOAD_IN] SolicitudId={solicitudId}; Rol={rolActivo}; UsuarioId={usuarioId}; Compania={companiaActiva}");

            if (!PuedeVerRecorrido(solicitudId, rolActivo, usuarioId, companiaActiva))
            {
                Trace.TraceWarning($"[RECORRIDO][ACCESS_DENY] SolicitudId={solicitudId}; Rol={rolActivo}; UsuarioId={usuarioId}; Motivo=Falta de permisos o inconsistencia de compania");
                return new List<AocrRecorridoEstadoViewModel>();
            }

            var hist = _procesoEstadoDao.ObtenerHistorialPorSolicitud(solicitudId) ?? new List<AocrProcesoEstadoHistorialRecord>();
            var actual = _estadoProcesoService.ObtenerEstadoActual(solicitudId);

            List<AocrRecorridoEstadoViewModel> list;
            if (hist.Count == 0 && actual != null)
            {
                list = ConstruirRecorridoInferido(solicitudId, actual);
                Trace.TraceInformation($"[RECORRIDO][LOAD_INFERRED] SolicitudId={solicitudId}; InferredRecords={list.Count}");
            }
            else
            {
                list = hist.Select(h => new AocrRecorridoEstadoViewModel
                {
                    Id = h.Id,
                    SolicitudId = h.SolicitudId,
                    OrdenRecaudacionId = h.OrdenRecaudacionId,
                    InspeccionId = h.InspeccionId,
                    InformeId = h.InformeId,
                    Fecha = h.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                    FechaCreacion = h.FechaCreacion,
                    Etapa = h.Etapa ?? ResolveMeta(h.EstadoNuevo).Etapa,
                    EstadoAnterior = FriendlyStateName(h.EstadoAnterior),
                    EstadoNuevo = FriendlyStateName(h.EstadoNuevo),
                    Accion = h.Accion,
                    RolUsuario = h.RolUsuario,
                    Usuario = h.UsuarioNombre,
                    RolResponsable = h.RolResponsable ?? ResolveMeta(h.EstadoNuevo).RolResponsable,
                    Responsable = h.ResponsableNombre ?? "Pendiente",
                    Observacion = h.Observacion,
                    EsEstadoActual = false
                }).ToList();

                if (actual != null && list.Count > 0)
                {
                    var last = list[list.Count - 1];
                    last.EsEstadoActual = true;
                }
            }

            if (list.Count == 0)
            {
                Trace.TraceWarning($"[RECORRIDO][LOAD_EMPTY] SolicitudId={solicitudId}; Motivo=Sin historial ni estado actual");
            }
            else
            {
                Trace.TraceInformation($"[RECORRIDO][LOAD_OK] SolicitudId={solicitudId}; TotalEstados={list.Count}");
            }

            return list;
        }

        public bool PuedeVerRecorrido(int solicitudId, string rolActivo, int usuarioId, string companiaActiva)
        {
            if (solicitudId <= 0) return false;

            string rol = (rolActivo ?? string.Empty).Trim().ToUpperInvariant();

            if (rol == "ADMINISTRADOR")
            {
                return true;
            }

            if (rol == "SOLICITANTE" || rol == "RT")
            {
                var compContext = new AocrCompaniaContextService();
                return compContext.ValidarSolicitudPerteneceACompaniaActiva(usuarioId, solicitudId, companiaActiva);
            }

            // Roles internos tienen permiso general de lectura del recorrido del trámite
            if (rol == "FINANCIERO" || rol == "COORDINADORFINANCIERO" ||
                rol == "COORDINADOR" || rol == "INSPECTOR" || rol == "INSPECTORTECNICO" ||
                rol == "DIRECCION" || rol == "DIRECCIONJEFATURATECNICA" || rol == "DIRDAC" || rol == "JEFATURA")
            {
                return true;
            }

            return false;
        }

        private List<AocrRecorridoEstadoViewModel> ConstruirRecorridoInferido(int solicitudId, AocrProcesoEstadoRecord actual)
        {
            var list = new List<AocrRecorridoEstadoViewModel>();
            if (actual == null) return list;

            string state = actual.EstadoActual;
            int maxIndex = -1;
            for (int i = 0; i < StandardStateSequence.Length; i++)
            {
                if (string.Equals(StandardStateSequence[i], state, StringComparison.OrdinalIgnoreCase))
                {
                    maxIndex = i;
                    break;
                }
            }

            if (maxIndex == -1)
            {
                maxIndex = 0;
            }

            DateTime baseDate = actual.FechaEstado.AddMinutes(-maxIndex * 15); // Spread them by 15 mins

            for (int i = 0; i <= maxIndex; i++)
            {
                string stateName = i < StandardStateSequence.Length ? StandardStateSequence[i] : state;
                var meta = ResolveMeta(stateName);

                list.Add(new AocrRecorridoEstadoViewModel
                {
                    Id = 0,
                    SolicitudId = solicitudId,
                    OrdenRecaudacionId = actual.OrdenRecaudacionId,
                    InspeccionId = actual.InspeccionId,
                    InformeId = actual.InformeId,
                    Fecha = baseDate.AddMinutes(i * 15).ToString("dd/MM/yyyy HH:mm"),
                    FechaCreacion = baseDate.AddMinutes(i * 15),
                    Etapa = meta.Etapa,
                    EstadoAnterior = i > 0 ? FriendlyStateName(StandardStateSequence[i - 1]) : null,
                    EstadoNuevo = FriendlyStateName(stateName),
                    Accion = meta.SiguienteAccion,
                    RolUsuario = meta.RolResponsable,
                    Usuario = "SISTEMA",
                    RolResponsable = meta.RolResponsable,
                    Responsable = "Pendiente",
                    Observacion = "Registro generado a partir del estado actual del trámite.",
                    EsEstadoActual = (i == maxIndex)
                });
            }

            return list;
        }

        public int ResolverSolicitudIdPorOrden(int ordenId)
        {
            return _procesoEstadoDao.ResolverSolicitudIdPorOrden(ordenId);
        }

        public int ResolverSolicitudIdPorInspeccion(int inspeccionId)
        {
            return _procesoEstadoDao.ResolverSolicitudIdPorInspeccion(inspeccionId);
        }

        public int ResolverSolicitudIdPorInforme(int informeId)
        {
            return _procesoEstadoDao.ResolverSolicitudIdPorInforme(informeId);
        }

        private static StateMeta ResolveMeta(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return new StateMeta { Etapa = "DESCONOCIDA", RolResponsable = "N/D", SiguienteAccion = "N/D" };
            }

            StateMeta meta;
            if (MetadataMap.TryGetValue(state.Trim(), out meta))
            {
                return meta;
            }

            return new StateMeta { Etapa = "PROCESO", RolResponsable = "Operativo", SiguienteAccion = "Avanzar trámite" };
        }

        private static string FriendlyStateName(string state)
        {
            if (string.IsNullOrWhiteSpace(state)) return string.Empty;
            return state.Replace("_", " ").ToUpperInvariant();
        }
    }
}
