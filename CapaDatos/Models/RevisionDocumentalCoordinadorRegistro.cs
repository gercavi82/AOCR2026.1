using System;

namespace CapaDatos.Models
{
    public sealed class RevisionDocumentalCoordinadorRegistro
    {
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        public int? InspectorOriginalId { get; set; }
        public int? InspectorConfirmadoId { get; set; }
        public int? CoordinadorId { get; set; }
        public int? DocumentoOficioId { get; set; }
        public string NumeroOficio { get; set; }
        public string Estado { get; set; }
        public string ObservacionInspector { get; set; }
        public string ObservacionCoordinador { get; set; }
        public DateTime? FechaFinalizacionInspector { get; set; }
        public DateTime? FechaDecisionCoordinador { get; set; }
        public DateTime? FechaHabilitacionLv { get; set; }
        public DateTime? FechaHabilitacionInforme { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaActualizacion { get; set; }
        public bool Activo { get; set; }
    }

    public static class EstadoRevisionDocumentalCoordinador
    {
        public const string FinalizadaInspector = "REVISION_DOCUMENTAL_FINALIZADA_INSPECTOR";
        public const string PendienteCoordinador = "PENDIENTE_REVISION_COORDINADOR";
        public const string ObservadaCoordinador = "OBSERVADA_POR_COORDINADOR";
        public const string AceptadaCoordinador = "ACEPTADA_POR_COORDINADOR";
    }

    public sealed class TransicionCoordinadorParams
    {
        public int SolicitudId { get; set; }
        public int CoordinadorId { get; set; }
        public string Accion { get; set; }
        public string EstadoSolicitudDestino { get; set; }
        public string EstadoRevisionDestino { get; set; }
        public string Observacion { get; set; }
        public string UsuarioLogin { get; set; }
        public int? ExpectedVersion { get; set; }
        public string EventKey { get; set; }
        public string EmailDestinatario { get; set; }
        public string EmailNombre { get; set; }
        public string EmailAsunto { get; set; }
        public string EmailCuerpo { get; set; }
        public string TipoNotificacion { get; set; }
    }

    public sealed class TransicionCoordinadorResultadoDAO
    {
        public bool Exitoso { get; set; }
        public bool ConflictoEstado { get; set; }
        public bool ConflictoVersion { get; set; }
        public string Mensaje { get; set; }
        public RevisionDocumentalCoordinadorRegistro Registro { get; set; }
    }
}
