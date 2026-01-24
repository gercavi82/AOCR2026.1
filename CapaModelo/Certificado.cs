using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapaModelo
{
    [Table("aocr_tbcertificado")]
    public class Certificado
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Display(Name = "Código Certificado")]
        public int CodigoCertificado { get; set; }

        [Required]
        [Display(Name = "Código Solicitud")]
        public int CodigoSolicitud { get; set; }

        [StringLength(50)]
        [Display(Name = "Número Certificado")]
        public string NumeroCertificado { get; set; }

        [StringLength(100)]
        [Display(Name = "Tipo")]
        public string Tipo { get; set; } // AOCR, ESPECIAL, CHARTER, REGULAR

        [StringLength(50)]
        [Display(Name = "Estado")]
        public string Estado { get; set; } // GENERADO, APROBADO, RECHAZADO, VENCIDO, ANULADO

        [Display(Name = "Fecha Emisión")]
        [DataType(DataType.DateTime)]
        public DateTime? FechaEmision { get; set; }

        [Display(Name = "Fecha Vencimiento")]
        [DataType(DataType.DateTime)]
        public DateTime? FechaVencimiento { get; set; }

        [StringLength(500)]
        [Display(Name = "Ruta Documento")]
        public string RutaDocumento { get; set; }

        [StringLength(1000)]
        [Display(Name = "Observaciones")]
        public string Observaciones { get; set; }

        [StringLength(100)]
        [Display(Name = "Emitido Por")]
        public string EmitidoPor { get; set; }

        [StringLength(100)]
        [Display(Name = "Aprobado Por")]
        public string AprobadoPor { get; set; }

        [Display(Name = "Fecha Creación")]
        public DateTime? CreatedAt { get; set; }

        [Display(Name = "Creado Por")]
        public int CreatedBy { get; set; }

        [Display(Name = "Fecha Actualización")]
        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "Actualizado Por")]
        public int UpdatedBy { get; set; }

        // Navegación
        [ForeignKey("CodigoSolicitud")]
        public virtual SolicitudAOCR Solicitud { get; set; }

        // =========================
        // Propiedades calculadas
        // =========================
        [NotMapped]
        [Display(Name = "¿Está Vencido?")]
        public bool EstaVencido => FechaVencimiento.HasValue && FechaVencimiento.Value < DateTime.Now;

        [NotMapped]
        [Display(Name = "¿Está Vigente?")]
        public bool EstaVigente => Estado == "APROBADO" && !EstaVencido;

        [NotMapped]
        [Display(Name = "Días Restantes")]
        public int? DiasRestantes
        {
            get
            {
                if (!FechaVencimiento.HasValue || EstaVencido)
                    return 0;

                return (int)(FechaVencimiento.Value - DateTime.Now).TotalDays;
            }
        }

        // =========================================================
        // ✅ ALIAS de compatibilidad (NO crean columnas nuevas)
        // =========================================================

        // Antes: RutaPdf
        [NotMapped]
        public string RutaPdf
        {
            get => RutaDocumento;
            set => RutaDocumento = value;
        }

        // Antes: FirmadoPor (en tu modelo real corresponde a EmitidoPor o AprobadoPor)
        // Usaremos EmitidoPor como "firmado" por quien emite.
        [NotMapped]
        public string FirmadoPor
        {
            get => EmitidoPor;
            set => EmitidoPor = value;
        }

        // Antes: VigenciaAnios (se puede calcular desde fechas)
        [NotMapped]
        public int VigenciaAnios
        {
            get
            {
                if (!FechaEmision.HasValue || !FechaVencimiento.HasValue) return 0;
                var days = (FechaVencimiento.Value - FechaEmision.Value).TotalDays;
                return (int)Math.Round(days / 365.0, 0);
            }
            set
            {
                // Si setean VigenciaAnios, ajustamos FechaVencimiento en base a FechaEmision
                var emision = FechaEmision ?? DateTime.Now;
                FechaEmision = emision;
                FechaVencimiento = emision.AddYears(value <= 0 ? 1 : value);
            }
        }

        // Antes: CodigoVerificacion (si NO hay columna en tu tabla, se puede guardar en Observaciones)
        // Formato: "VERIF:<codigo>"
        [NotMapped]
        public string CodigoVerificacion
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Observaciones)) return null;

                // Buscar "VERIF:" dentro de Observaciones (simple y seguro)
                var tag = "VERIF:";
                var idx = Observaciones.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return null;

                var start = idx + tag.Length;
                // tomar hasta salto de línea o hasta 64 chars
                var end = Observaciones.IndexOfAny(new[] { '\r', '\n' }, start);
                var value = (end > start) ? Observaciones.Substring(start, end - start) : Observaciones.Substring(start);
                value = value.Trim();

                return value.Length > 0 ? value : null;
            }
            set
            {
                var tag = "VERIF:";
                var limpio = (value ?? "").Trim();

                // eliminar tag previo si existía
                if (!string.IsNullOrWhiteSpace(Observaciones))
                {
                    var idx = Observaciones.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        // corta desde el tag hasta fin de línea
                        var start = idx;
                        var end = Observaciones.IndexOfAny(new[] { '\r', '\n' }, idx);
                        Observaciones = (end > idx)
                            ? (Observaciones.Remove(start, end - start)).Trim()
                            : Observaciones.Substring(0, idx).Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(limpio)) return;

                // anexar
                if (string.IsNullOrWhiteSpace(Observaciones))
                    Observaciones = $"{tag}{limpio}";
                else
                    Observaciones = $"{Observaciones}\n{tag}{limpio}";
            }
        }

        // =========================
        // Métodos de dominio
        // =========================
        public void Aprobar(string aprobadoPor, int usuarioId)
        {
            Estado = "APROBADO";
            AprobadoPor = aprobadoPor;
            UpdatedBy = usuarioId;
            UpdatedAt = DateTime.Now;
        }

        public void Rechazar(string motivo, int usuarioId)
        {
            Estado = "RECHAZADO";
            Observaciones = $"Rechazado: {motivo} - {DateTime.Now:dd/MM/yyyy HH:mm}";
            UpdatedBy = usuarioId;
            UpdatedAt = DateTime.Now;
        }

        public void Anular(string motivo, int usuarioId)
        {
            Estado = "ANULADO";
            Observaciones = $"Anulado: {motivo} - {DateTime.Now:dd/MM/yyyy HH:mm}";
            UpdatedBy = usuarioId;
            UpdatedAt = DateTime.Now;
        }

        public bool PuedeRenovar()
        {
            return (Estado == "APROBADO" && EstaVencido) ||
                   (Estado == "VENCIDO" && FechaVencimiento.HasValue &&
                    FechaVencimiento.Value > DateTime.Now.AddMonths(-3)); // 3 meses de gracia
        }
    }
}
