using System;

namespace CapaNegocio.DTOs
{
    public static class TiposDocumentoFirmaInstitucional
    {
        public const string Aocr = "RECONOCIMIENTO";
        public const string Condiciones = "CONDICIONES_LIMITACIONES";
    }

    public sealed class FirmarDocumentoInstitucionalRequest
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public string TipoDocumento { get; set; }
        public int UsuarioId { get; set; }
        public long VersionExpediente { get; set; }
        public string CorrelationId { get; set; }
        public string Ip { get; set; }
        public string ClaveIdempotencia { get; set; }
    }

    public sealed class ResultadoFirmaDocumento
    {
        public bool Exitoso { get; set; }
        public bool YaProcesado { get; set; }
        public int CodigoHttp { get; set; }
        public string Mensaje { get; set; }
        public int FirmaId { get; set; }
        public int DocumentoId { get; set; }
        public int PdfOrigenId { get; set; }
        public int VersionDocumento { get; set; }
        public string TipoDocumento { get; set; }
        public string EstadoDocumento { get; set; }
        public string EstadoExpediente { get; set; }
        public string RutaPdfFirmado { get; set; }
        public string HashPdfFirmado { get; set; }
        public long TamanioPdfFirmado { get; set; }
        public DateTime? FechaFirma { get; set; }
    }

    public sealed class ResultadoValidacionFirma
    {
        public bool Valido { get; set; }
        public int CodigoHttp { get; set; }
        public string Mensaje { get; set; }
        public int DocumentoId { get; set; }
        public int PdfOrigenId { get; set; }
        public int VersionDocumento { get; set; }
        public PerfilFirmanteDto Perfil { get; set; }
        public ConfiguracionPosicionFirmaDto Posicion { get; set; }
    }

    public sealed class EstadoFirmasExpedienteDto
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public string EstadoAocr { get; set; }
        public string EstadoCondiciones { get; set; }
        public bool AocrFirmadoDgac { get; set; }
        public bool CondicionesFirmadasDcav { get; set; }
        public bool AmbasFirmasCompletas { get; set; }
        public string EstadoCentral { get; set; }
    }

    public sealed class PerfilFirmanteDto
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; }
        public string Cargo { get; set; }
        public int CodigoRol { get; set; }
        public string Rol { get; set; }
        public int FirmaImagenId { get; set; }
        public string RutaInternaFirma { get; set; }
        public string HashFirma { get; set; }
        public bool Activo { get; set; }
        public DateTime? VigenteDesde { get; set; }
        public DateTime? VigenteHasta { get; set; }
        public bool AutorizadoParaDocumento { get; set; }
    }

    public sealed class ConfiguracionPosicionFirmaDto
    {
        public int ConfiguracionId { get; set; }
        public string TipoDocumento { get; set; }
        public int VersionPlantilla { get; set; }
        public int Pagina { get; set; }
        public decimal XRatio { get; set; }
        public decimal YRatio { get; set; }
        public decimal AnchoRatio { get; set; }
        public decimal AltoRatio { get; set; }
        public decimal MargenRatio { get; set; }
        public bool MostrarQr { get; set; }
        public string Alineacion { get; set; }
        public decimal NombreYRatio { get; set; }
        public decimal CargoYRatio { get; set; }
        public decimal FechaYRatio { get; set; }
        public decimal QrXRatio { get; set; }
        public decimal QrYRatio { get; set; }
        public decimal QrTamanioRatio { get; set; }
    }
}
