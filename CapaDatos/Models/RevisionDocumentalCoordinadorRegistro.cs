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
}
