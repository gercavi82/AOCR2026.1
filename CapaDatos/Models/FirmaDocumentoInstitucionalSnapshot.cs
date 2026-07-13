namespace CapaDatos.Models
{
    public sealed class FirmaDocumentoInstitucionalSnapshot
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public int InformeId { get; set; }
        public long VersionExpediente { get; set; }
        public string EstadoCentral { get; set; }
        public int DocumentoId { get; set; }
        public int VersionDocumento { get; set; }
        public string EstadoDocumento { get; set; }
        public string TipoDocumento { get; set; }
        public string CompaniaId { get; set; }
        public int PdfOrigenId { get; set; }
        public string RutaPdfOrigen { get; set; }
        public string HashPdfOrigen { get; set; }
        public long TamanioPdfOrigen { get; set; }
        public string ContentType { get; set; }
        public int AocrId { get; set; }
        public int AocrPdfId { get; set; }
        public int VersionAocr { get; set; }
        public string EstadoAocr { get; set; }
        public int CondicionesId { get; set; }
        public int CondicionesPdfId { get; set; }
        public int VersionCondiciones { get; set; }
        public string EstadoCondiciones { get; set; }
    }
}
