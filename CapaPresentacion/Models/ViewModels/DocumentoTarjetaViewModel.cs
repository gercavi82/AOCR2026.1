namespace CapaPresentacion.Models.ViewModels
{
    public class DocumentoTarjetaViewModel
    {
        public int CodigoDocumento { get; set; }
        public string NombreArchivo { get; set; }
        public string NombreArchivoCompleto { get; set; }
        public string TipoDocumentoVisible { get; set; }
        public string ConceptoVisible { get; set; }
        public string AeropuertosVisible { get; set; }
        public string FechaCargaVisible { get; set; }
        public string EstadoVisible { get; set; }
        public string EstadoBadgeCss { get; set; }
        public string TamanoVisible { get; set; }
        public string UrlDescarga { get; set; }
        public bool MostrarDescarga { get; set; }
        public string IconoCss { get; set; }
    }
}
