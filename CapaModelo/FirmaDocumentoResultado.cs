namespace CapaModelo
{
    public sealed class FirmaDocumentoResultado
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; }
        public string RutaOrigen { get; set; }
        public string RutaFirmada { get; set; }
        public string HashSha256 { get; set; }
        public long Bytes { get; set; }
        public string EstadoSolicitudNuevo { get; set; }
        public string EstadoAocrNuevo { get; set; }
    }
}
