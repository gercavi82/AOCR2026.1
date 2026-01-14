public class Certificado
{
    public int CodigoCertificado { get; set; }
    public int CodigoSolicitud { get; set; }
    public string NumeroCertificado { get; set; }

    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public int VigenciaAnios { get; set; } = 2;

    public string Estado { get; set; }
    public string CondicionesEspeciales { get; set; }

    // Firma digital
    public string FirmadoPor { get; set; } // Obligatorio
    public string RutaPdf { get; set; }
    public string CodigoVerificacion { get; set; }

    // Auditoría (puedes extender si usas estos campos)
    public DateTime? CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string UpdatedBy { get; set; }
}
