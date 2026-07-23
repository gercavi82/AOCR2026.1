using System;
using System.Diagnostics;
using CapaDatos.DAOs;
using CapaDatos.Models;

namespace CapaNegocio.Services
{
    public sealed class RevisionDocumentalCoordinadorResultado
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; }
        public RevisionDocumentalCoordinadorRegistro Registro { get; set; }
    }

    public sealed class RevisionDocumentalCoordinadorService
    {
        private readonly RevisionDocumentalCoordinadorDAO _dao = new RevisionDocumentalCoordinadorDAO();
        private readonly UsuarioInternoRTDAO _usuarioDao = new UsuarioInternoRTDAO();

        public RevisionDocumentalCoordinadorRegistro ObtenerPorSolicitud(int solicitudId)
        {
            return _dao.ObtenerPorSolicitud(solicitudId);
        }

        public RevisionDocumentalCoordinadorResultado FinalizarRevisionDocumentalInspector(
            int solicitudId,
            int inspectorId,
            string observacionGeneral)
        {
            Trace.TraceInformation("[REV_DOC][FINALIZAR_INSPECTOR_IN] SolicitudId={0}; InspectorId={1};", solicitudId, inspectorId);
            var observacion = NormalizarObservacion(observacionGeneral);
            if (observacion == null)
            {
                return Error("La observacion general no puede contener HTML ni superar 2000 caracteres.");
            }

            var registro = _dao.RegistrarFinalizacionInspector(solicitudId, inspectorId, observacion);
            Trace.TraceInformation(
                "[REV_DOC][OBSERVACION_GENERAL] SolicitudId={0}; TieneObservacion={1};",
                solicitudId,
                !string.IsNullOrWhiteSpace(observacion));

            return new RevisionDocumentalCoordinadorResultado
            {
                Ok = registro != null,
                Mensaje = registro != null
                    ? "Revision documental enviada a Coordinacion. LV e Informe Tecnico permanecen bloqueados."
                    : "No fue posible enviar la revision documental a Coordinacion.",
                Registro = registro
            };
        }

        public RevisionDocumentalCoordinadorResultado Observar(
            int solicitudId,
            int coordinadorId,
            string observacion)
        {
            var texto = NormalizarObservacion(observacion);
            if (string.IsNullOrWhiteSpace(texto))
            {
                return Error("La observacion del Coordinador es obligatoria.");
            }

            var actual = _dao.ObtenerPorSolicitud(solicitudId);
            if (actual == null || actual.DocumentoOficioId.GetValueOrDefault() <= 0)
            {
                return Error("No existe un oficio generado para esta revision documental.");
            }

            Trace.TraceInformation("[COORD][REV_DOC_DECISION_IN] SolicitudId={0}; Decision=OBSERVAR;", solicitudId);
            var ok = _dao.RegistrarDecision(
                solicitudId,
                coordinadorId,
                EstadoRevisionDocumentalCoordinador.ObservadaCoordinador,
                null,
                texto);
            if (ok)
            {
                Trace.TraceInformation("[COORD][REV_DOC_OBSERVADA] SolicitudId={0}; CoordinadorId={1};", solicitudId, coordinadorId);
            }

            return new RevisionDocumentalCoordinadorResultado
            {
                Ok = ok,
                Mensaje = ok ? "La revision fue devuelta al Inspector con la observacion registrada." : "No fue posible observar la revision documental.",
                Registro = ok ? _dao.ObtenerPorSolicitud(solicitudId) : actual
            };
        }

        public RevisionDocumentalCoordinadorResultado Aceptar(
            int solicitudId,
            int coordinadorId,
            int inspectorId,
            string observacion)
        {
            var texto = NormalizarObservacion(observacion);
            if (texto == null) return Error("La observacion no puede contener HTML ni superar 2000 caracteres.");

            var actual = _dao.ObtenerPorSolicitud(solicitudId);
            if (actual == null || actual.DocumentoOficioId.GetValueOrDefault() <= 0)
            {
                return Error("No existe un oficio generado para esta revision documental.");
            }

            var inspector = _usuarioDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(inspectorId);
            if (inspector == null || !inspector.Activo)
            {
                return Error("El inspector seleccionado no existe o ya no esta activo.");
            }

            var idPersistencia = inspector.UsuarioId.GetValueOrDefault() > 0
                ? inspector.UsuarioId.Value
                : inspector.TecnicoId.GetValueOrDefault();
            if (idPersistencia <= 0) return Error("El inspector seleccionado no tiene un identificador institucional valido.");

            Trace.TraceInformation("[COORD][REV_DOC_DECISION_IN] SolicitudId={0}; Decision=ACEPTAR;", solicitudId);
            Trace.TraceInformation("[LV_INFORME][HABILITAR_IN] SolicitudId={0}; InspectorId={1};", solicitudId, idPersistencia);
            var ok = _dao.RegistrarDecision(
                solicitudId,
                coordinadorId,
                EstadoRevisionDocumentalCoordinador.AceptadaCoordinador,
                idPersistencia,
                texto,
                inspector.UsuarioLogin,
                inspector.NombreVisual,
                inspector.Tipo);

            if (ok)
            {
                var mantenido = actual.InspectorOriginalId.GetValueOrDefault() == idPersistencia;
                Trace.TraceInformation(
                    mantenido ? "[COORD][INSPECTOR_MANTENIDO] SolicitudId={0}; InspectorId={1};" : "[COORD][INSPECTOR_REASIGNADO] SolicitudId={0}; InspectorAnteriorId={1}; InspectorNuevoId={2};",
                    mantenido ? new object[] { solicitudId, idPersistencia } : new object[] { solicitudId, actual.InspectorOriginalId.GetValueOrDefault(), idPersistencia });
                Trace.TraceInformation("[LV_INFORME][HABILITAR_OK] SolicitudId={0}; InspectorId={1};", solicitudId, idPersistencia);
            }

            return new RevisionDocumentalCoordinadorResultado
            {
                Ok = ok,
                Mensaje = ok
                    ? "Revision aceptada. LV e Informe Tecnico fueron habilitados exclusivamente para el inspector confirmado."
                    : "No fue posible aceptar la revision ni habilitar la fase operativa.",
                Registro = ok ? _dao.ObtenerPorSolicitud(solicitudId) : actual
            };
        }

        public bool RequiereAceptacionCoordinador(int solicitudId)
        {
            return solicitudId > 0 && _dao.ObtenerPorSolicitud(solicitudId) != null;
        }

        public bool EstaAceptadaParaInspector(int solicitudId, int inspectorId)
        {
            var registro = _dao.ObtenerPorSolicitud(solicitudId);
            return registro != null
                && string.Equals(registro.Estado, EstadoRevisionDocumentalCoordinador.AceptadaCoordinador, StringComparison.OrdinalIgnoreCase)
                && registro.DocumentoOficioId.GetValueOrDefault() > 0
                && registro.InspectorConfirmadoId.GetValueOrDefault() == inspectorId
                && registro.FechaHabilitacionLv.HasValue
                && registro.FechaHabilitacionInforme.HasValue;
        }

        public static string NormalizarObservacion(string valor)
        {
            var texto = (valor ?? string.Empty).Trim();
            if (texto.Length > 2000 || texto.IndexOf('<') >= 0 || texto.IndexOf('>') >= 0)
            {
                return null;
            }
            return texto;
        }

        private static RevisionDocumentalCoordinadorResultado Error(string mensaje)
        {
            return new RevisionDocumentalCoordinadorResultado { Ok = false, Mensaje = mensaje };
        }
    }
}
