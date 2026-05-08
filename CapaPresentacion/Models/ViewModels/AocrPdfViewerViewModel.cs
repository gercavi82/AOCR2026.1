namespace CapaPresentacion.Models.ViewModels
{
    public class AocrPdfViewerViewModel
    {
        public string PdfUrl { get; set; }
        public string Titulo { get; set; }
        public string Subtitulo { get; set; }
        public bool PermitirDescarga { get; set; }
        public bool PermitirImpresion { get; set; }
        public bool MostrarToolbar { get; set; }
        public string ContenedorId { get; set; }
        public string EstadoDocumento { get; set; }
        public string MensajeAyuda { get; set; }
        public bool PantallaCompleta { get; set; }
        public string DescargarUrl { get; set; }
    }
}
