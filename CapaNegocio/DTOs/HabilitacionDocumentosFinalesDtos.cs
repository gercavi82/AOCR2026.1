namespace CapaNegocio.DTOs
{
    public sealed class HabilitarDocumentosRequest
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public int InformeTecnicoId { get; set; }
        public int UsuarioDcavId { get; set; }
        public string Rol { get; set; }
        public string EstadoEsperado { get; set; }
        public string ClaveIdempotencia { get; set; }
        public long VersionRegistro { get; set; }
        public int VersionInforme { get; set; }
        public string Ip { get; set; }
        public string CorrelationId { get; set; }
    }

    public sealed class ResultadoHabilitacionDocumentos
    {
        public bool Exitoso { get; set; }
        public bool YaProcesado { get; set; }
        public int Codigo { get; set; }
        public string Mensaje { get; set; }
        public int AocrId { get; set; }
        public int CondicionesId { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
    }

    public sealed class BorradorDocumentoRequest
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public string CodigoCompania { get; set; }
        public int InspectorId { get; set; }
        public int UsuarioCreadorId { get; set; }
    }

    public sealed class ResultadoBorradorDocumento
    {
        public bool Exitoso { get; set; }
        public bool Creado { get; set; }
        public CapaModelo.AocrDocumentoGenerado Documento { get; set; }
    }
}
