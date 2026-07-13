namespace CapaNegocio.DTOs
{
    public sealed class DecisionDocumentosDcavRequest
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public long VersionExpediente { get; set; }
        public int AocrId { get; set; }
        public int VersionAocr { get; set; }
        public int AocrPdfId { get; set; }
        public int CondicionesId { get; set; }
        public int VersionCondiciones { get; set; }
        public int CondicionesPdfId { get; set; }
        public bool ObservarAocr { get; set; }
        public bool ObservarCondiciones { get; set; }
        public string SeccionCampo { get; set; }
        public string Observacion { get; set; }
        public int UsuarioId { get; set; }
        public string Rol { get; set; }
        public string Ip { get; set; }
        public string CorrelationId { get; set; }
    }

    public sealed class RevisionDocumentosDcavResultado
    {
        public bool Exitoso { get; set; }
        public bool YaProcesado { get; set; }
        public int Codigo { get; set; }
        public string Mensaje { get; set; }
        public string EstadoNuevo { get; set; }
    }
}
