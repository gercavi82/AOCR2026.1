namespace CapaDatos.Models
{
    public sealed class HabilitacionDocumentosSnapshot
    {
        public int SolicitudId { get; set; }
        public string EstadoSolicitud { get; set; }
        public bool SolicitudActiva { get; set; }
        public string CodigoCompania { get; set; }
        public int InspeccionId { get; set; }
        public int InspectorId { get; set; }
        public string EstadoInspeccion { get; set; }
        public string ResultadoInspeccion { get; set; }
        public int InformeId { get; set; }
        public int VersionInforme { get; set; }
        public string EstadoInforme { get; set; }
        public string ResultadoInforme { get; set; }
        public bool InformeFinalizado { get; set; }
        public bool InformeFirmado { get; set; }
        public string RutaInformeFirmado { get; set; }
        public string HashInforme { get; set; }
        public bool InformeVigente { get; set; }
        public int ListaId { get; set; }
        public bool ListaFinalizada { get; set; }
        public bool ListaFirmada { get; set; }
        public string RutaListaFirmada { get; set; }
        public string HashLista { get; set; }
        public string EstadoCentral { get; set; }
        public long VersionRegistro { get; set; }
    }

    public sealed class HabilitacionIdempotenciaRecord
    {
        public string Clave { get; set; }
        public int SolicitudId { get; set; }
        public int AocrId { get; set; }
        public int CondicionesId { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Resultado { get; set; }
    }
}
