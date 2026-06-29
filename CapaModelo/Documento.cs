using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapaModelo
{
    [Table("Documentos")] // Especificar nombre de tabla
    public class Documento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Display(Name = "Código Documento")]
        public int CodigoDocumento { get; set; }

        [Required(ErrorMessage = "El código de solicitud es requerido")]
        [Display(Name = "Código Solicitud")]
        public int CodigoSolicitud { get; set; }

        [Required(ErrorMessage = "El tipo de documento es requerido")]
        [StringLength(100, ErrorMessage = "El tipo no puede exceder 100 caracteres")]
        [Display(Name = "Tipo de Documento")]
        public string TipoDocumento { get; set; }

        [Required(ErrorMessage = "El nombre del archivo es requerido")]
        [StringLength(255, ErrorMessage = "El nombre no puede exceder 255 caracteres")]
        [Display(Name = "Nombre del Archivo")]
        public string NombreArchivo { get; set; }

        [Required(ErrorMessage = "La ruta del archivo es requerida")]
        [StringLength(500, ErrorMessage = "La ruta no puede exceder 500 caracteres")]
        [Display(Name = "Ruta Guardada")]
        public string RutaGuardada { get; set; }

        [Required(ErrorMessage = "La extensión es requerida")]
        [StringLength(20, ErrorMessage = "La extensión no puede exceder 20 caracteres")]
        [Display(Name = "Extensión")]
        public string Extension { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "El tamaño debe ser mayor a 0")]
        [Display(Name = "Tamaño (Bytes)")]
        public long? TamanoBytes { get; set; }

        [StringLength(50, ErrorMessage = "El estado no puede exceder 50 caracteres")]
        [Display(Name = "Estado")]
        public string Estado { get; set; } = "PENDIENTE"; // Valor por defecto

        [Display(Name = "Validado")]
        public bool? Validado { get; set; } = false; // Valor por defecto

        [Display(Name = "Fecha de Carga")]
        [DataType(DataType.DateTime)]
        public DateTime? FechaCarga { get; set; } = DateTime.Now; // Valor por defecto

        [Display(Name = "Fecha de Validación")]
        [DataType(DataType.DateTime)]
        public DateTime? FechaValidacion { get; set; }

        [StringLength(255, ErrorMessage = "El usuario validador no puede exceder 255 caracteres")]
        [Display(Name = "Validado por")]
        public string ValidadoPor { get; set; }

        [StringLength(1000, ErrorMessage = "Las observaciones no pueden exceder 1000 caracteres")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Observaciones")]
        public string Observaciones { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La versión debe ser mayor a 0")]
        [Display(Name = "Versión")]
        public int? Version { get; set; } = 1; // Valor por defecto

        [Required(ErrorMessage = "El usuario de registro es requerido")]
        [StringLength(100, ErrorMessage = "El usuario no puede exceder 100 caracteres")]
        [Display(Name = "Usuario Registro")]
        public string UsuarioRegistro { get; set; }

        [NotMapped]
        public string DecisionRevision { get; set; }

        [NotMapped]
        public string ObservacionRevision { get; set; }

        [NotMapped]
        public DateTime? FechaRevision { get; set; }

        [NotMapped]
        public int? CodigoUsuarioRevisor { get; set; }

        [NotMapped]
        public string NombreUsuarioRevisor { get; set; }

        [NotMapped]
        public string EstadoRevisionVisible { get; set; }

        [NotMapped]
        public string OperadoraEae { get; set; }

        [NotMapped]
        public string TipoDocumentoCodigoCanonico { get; set; }

        [NotMapped]
        public string TipoDocumentoNombre { get; set; }

        [NotMapped]
        public int OrdenVisual { get; set; }

        [NotMapped]
        public string ResumenTrazabilidad { get; set; }

        [NotMapped]
        public bool PuedeVisualizar { get; set; }

        [NotMapped]
        public bool PuedeDescargar { get; set; }

        [NotMapped]
        public string UrlVisualizar { get; set; }

        [NotMapped]
        public string UrlDescargar { get; set; }

        [NotMapped]
        public string NombreArchivoGuardado { get; set; }

        [NotMapped]
        public string NombreArchivoOriginal { get; set; }

        [NotMapped]
        public string NombreArchivoVisible { get; set; }

        [NotMapped]
        public string NombreArchivoFisico { get; set; }

        [NotMapped]
        public string NombreOriginal
        {
            get => NombreArchivoOriginal;
            set => NombreArchivoOriginal = value;
        }

        [NotMapped]
        public string NombreVisible
        {
            get => NombreArchivoVisible;
            set => NombreArchivoVisible = value;
        }

        [NotMapped]
        public string NombreFisico
        {
            get => NombreArchivoFisico;
            set => NombreArchivoFisico = value;
        }

        [NotMapped]
        public bool PuedeEditarEstado { get; set; }

        // ====== PROPIEDADES DE NAVEGACIÓN ======
        [ForeignKey("CodigoSolicitud")]
        public virtual SolicitudAOCR Solicitud { get; set; }

        // ====== PROPIEDADES CALCULADAS ======
        [NotMapped]
        [Display(Name = "Tamaño Formateado")]
        public string TamañoFormateado
        {
            get
            {
                if (!TamanoBytes.HasValue) return "0 Bytes";

                long bytes = TamanoBytes.Value;
                string[] sizes = { "Bytes", "KB", "MB", "GB" };
                int order = 0;
                while (bytes >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    bytes = bytes / 1024;
                }
                return $"{bytes:0.##} {sizes[order]}";
            }
        }

        [NotMapped]
        [Display(Name = "¿Es Válido?")]
        public bool EsValido => Validado == true && Estado == "VALIDADO";

        [NotMapped]
        [Display(Name = "¿Requiere Subsanación?")]
        public bool RequiereSubsanacion => Estado == "SUBSANACION" || Estado == "RECHAZADO";

        // ====== ALIAS para compatibilidad con código viejo ======
        [NotMapped]
        public string RutaArchivo
        {
            get => RutaGuardada;
            set => RutaGuardada = value;
        }

        [NotMapped]
        public long? TamanioArchivo
        {
            get => TamanoBytes;
            set => TamanoBytes = value;
        }

        [NotMapped]
        public DateTime? FechaSubida
        {
            get => FechaCarga;
            set => FechaCarga = value;
        }

        [NotMapped]
        public string ExtensionArchivo
        {
            get => Extension;
            set => Extension = value;
        }

        // ====== MÉTODOS ======
        public void MarcarComoValidado(string usuarioValidador = null)
        {
            Validado = true;
            Estado = "VALIDADO";
            FechaCarga = DateTime.Now;
            if (!string.IsNullOrEmpty(usuarioValidador))
                Observaciones = $"Validado por: {usuarioValidador} - {DateTime.Now:dd/MM/yyyy HH:mm}";
        }

        public void MarcarComoRechazado(string motivo, string usuario = null)
        {
            Validado = false;
            Estado = "RECHAZADO";
            Observaciones = $"Rechazado: {motivo}" +
                           (string.IsNullOrEmpty(usuario) ? "" : $" por {usuario}") +
                           $" - {DateTime.Now:dd/MM/yyyy HH:mm}";
        }

        public void MarcarParaSubsanacion(string motivo, string usuario = null)
        {
            Estado = "SUBSANACION";
            Observaciones = $"Requiere subsanación: {motivo}" +
                           (string.IsNullOrEmpty(usuario) ? "" : $" - {usuario}") +
                           $" - {DateTime.Now:dd/MM/yyyy HH:mm}";
        }

        public void IncrementarVersion()
        {
            Version = (Version ?? 0) + 1;
            FechaCarga = DateTime.Now;
        }

        // ====== SOBRECARGA DE MÉTODOS ======
        public override string ToString()
        {
            return $"{NombreArchivo} ({TipoDocumento}) - {Estado}";
        }

        public bool EsTipoPermitido()
        {
            string[] tiposPermitidos = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };
            return Array.Exists(tiposPermitidos, tipo =>
                Extension?.ToLower().Contains(tipo.Replace(".", "")) == true);
        }

        public bool ExcedeTamañoMaximo(long tamañoMaximoBytes = 10 * 1024 * 1024) // 10MB por defecto
        {
            return TamanoBytes.HasValue && TamanoBytes.Value > tamañoMaximoBytes;
        }
    }

    public class RevisionDocumentalDetalle
    {
        public int CodigoDocumento { get; set; }
        public string Decision { get; set; }
        public string Observacion { get; set; }
        public int? CodigoUsuarioRevisor { get; set; }
        public DateTime? FechaRevision { get; set; }
        public string CreatedBy { get; set; }
        public string NombreUsuarioRevisor { get; set; }
    }

    public class EstadoRevisionDocumental
    {
        public int CodigoSolicitud { get; set; }
        public int TotalDocumentosVigentes { get; set; }
        public int DocumentosAceptados { get; set; }
        public int DocumentosPendientesRevision { get; set; }
        public int DocumentosObservadosDevueltos { get; set; }
        public int DocumentosSubsanadosPendientes { get; set; }
        public bool TieneDocumentosObservados { get; set; }
        public bool TieneDocumentosSubsanadosPendientes { get; set; }
        public bool TienePendientes { get; set; }
        public bool DocumentacionAprobada { get; set; }
        public string MensajeBloqueoDocumental { get; set; }
        public string FlujoDocumentalCodigo { get; set; }
        public string FlujoDocumentalNombre { get; set; }
        public string ResponsableActual { get; set; }
        public bool VisibleEnBandejaInspector { get; set; }
        public bool VisibleEnBandejaCoordinador { get; set; }
        public bool VisibleEnBandejaRt { get; set; }

        public EstadoRevisionDocumental()
        {
            MensajeBloqueoDocumental = string.Empty;
            FlujoDocumentalCodigo = string.Empty;
            FlujoDocumentalNombre = string.Empty;
            ResponsableActual = string.Empty;
        }
    }

    public class ResultadoCierreDocumentalDto
    {
        public bool Ok { get; set; }
        public bool Cerrada { get; set; }
        public bool YaCerrada { get; set; }
        public bool HabilitaLv { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Mensaje { get; set; }
        public string MotivoSkip { get; set; }
        public EstadoRevisionDocumental EstadoRevision { get; set; }

        public ResultadoCierreDocumentalDto()
        {
            EstadoAnterior = string.Empty;
            EstadoNuevo = string.Empty;
            Mensaje = string.Empty;
            MotivoSkip = string.Empty;
        }
    }

    // ====== ENUMS PARA ESTADOS Y TIPOS ======
    public enum EstadoDocumento
    {
        PENDIENTE,
        VALIDADO,
        RECHAZADO,
        SUBSANACION,
        OBSERVADO,
        APROBADO
    }

    public enum TipoDocumentoAOCR
    {
        // Documentos principales
        SOLICITUD_FIRMADA,
        CERTIFICADO_OPERADOR,
        PLAN_VUELO,
        SEGURO_AERONAVEGACION,
        CERTIFICADO_AERONAVEGABILIDAD,
        LICENCIA_TRIPULACION,

        // Documentos adicionales
        CARTA_PODER,
        CERTIFICADO_REGISTRO,
        AUTORIZACION_ESPECIAL,
        DOCUMENTO_IDENTIFICACION,

        // Subsanaciones
        SUBSANACION,
        ACLARACION,

        // Otros
        OTRO
    }

    // ====== CLASE PARA VALIDACIÓN ======
    public static class ValidadorDocumentos
    {
        public static (bool Valido, string Mensaje) ValidarDocumento(Documento documento)
        {
            if (documento == null)
                return (false, "El documento no puede ser nulo");

            if (string.IsNullOrWhiteSpace(documento.NombreArchivo))
                return (false, "El nombre del archivo es requerido");

            if (string.IsNullOrWhiteSpace(documento.RutaGuardada))
                return (false, "La ruta del archivo es requerida");

            if (!documento.TamanoBytes.HasValue || documento.TamanoBytes.Value <= 0)
                return (false, "El tamaño del archivo debe ser mayor a 0");

            if (documento.ExcedeTamañoMaximo())
                return (false, $"El archivo excede el tamaño máximo permitido (10MB)");

            if (!documento.EsTipoPermitido())
                return (false, $"Tipo de archivo no permitido. Extensiones válidas: PDF, DOC, DOCX, XLS, XLSX, JPG, JPEG, PNG");

            return (true, "Documento válido");
        }

        public static string ObtenerDescripcionTipo(string tipoDocumento)
        {
            if (string.IsNullOrEmpty(tipoDocumento))
                return "Documento no especificado";

            switch (tipoDocumento.ToUpper())
            {
                case "SOLICITUD_FIRMADA":
                    return "Solicitud firmada por el operador";
                case "CERTIFICADO_OPERADOR":
                    return "Certificado de operador aéreo";
                case "PLAN_VUELO":
                    return "Plan de vuelo detallado";
                case "SEGURO_AERONAVEGACION":
                    return "Póliza de seguro de aeronavegación";
                case "CERTIFICADO_AERONAVEGABILIDAD":
                    return "Certificado de aeronavegabilidad";
                case "LICENCIA_TRIPULACION":
                    return "Licencias de la tripulación";
                case "CARTA_PODER":
                    return "Carta poder del representante legal";
                case "CERTIFICADO_REGISTRO":
                    return "Certificado de registro de aeronave";
                case "AUTORIZACION_ESPECIAL":
                    return "Autorización especial para el vuelo";
                case "DOCUMENTO_IDENTIFICACION":
                    return "Documento de identificación";
                case "SUBSANACION":
                    return "Documento de subsanación";
                case "ACLARACION":
                    return "Documento de aclaración";
                case "OTRO":
                    return "Otro documento";
                default:
                    return "Documento no especificado";
            }
        }
    }
}
