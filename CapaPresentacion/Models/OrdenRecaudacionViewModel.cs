using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System;
using System.Collections.Generic;
namespace CapaPresentacion.Models.ViewModels
{
    public class OrdenRecaudacionViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "El concepto principal es obligatorio.")]
        [Display(Name = "Concepto Principal")]
        [StringLength(50, ErrorMessage = "El código del concepto no puede exceder los 50 caracteres.")]
        [RegularExpression(@"^[A-Z_]+$", ErrorMessage = "Solo se permiten letras mayúsculas y guiones bajos.")]
        public string ConceptoPrincipalCodigo { get; set; }

        [Display(Name = "Código de Solicitud")]
        [Range(0, int.MaxValue, ErrorMessage = "El código de solicitud debe ser un número positivo.")]
        [DefaultValue(0)]
        public int? CodigoSolicitud { get; set; }

        [Required(ErrorMessage = "El número de estaciones es obligatorio.")]
        [Display(Name = "Estaciones a Inspeccionar")]
        [Range(0, 50, ErrorMessage = "El número de estaciones debe estar entre 0 y 50.")]
        [DefaultValue(0)]
        public int Estaciones { get; set; }

        [Required(ErrorMessage = "El número de días es obligatorio.")]
        [Display(Name = "Días de Viáticos")]
        [Range(0, 30, ErrorMessage = "El número de días debe estar entre 0 y 30.")]
        [DefaultValue(0)]
        public int Dias { get; set; }

        [Display(Name = "Observaciones / Referencia")]
        [StringLength(500, ErrorMessage = "Las observaciones no pueden exceder los 500 caracteres.")]
        [DataType(DataType.MultilineText)]
        public string Observacion { get; set; }

        [ScaffoldColumn(false)]
        public string TokenSeguridad { get; set; }

        [ScaffoldColumn(false)]
        public string IpCliente { get; set; }

        [ScaffoldColumn(false)]
        public DateTime FechaSolicitud { get; set; } = DateTime.Now;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var resultados = new List<ValidationResult>();

            // Validación personalizada: Si hay estaciones, debe haber al menos un día
            if (Estaciones > 0 && Dias == 0)
            {
                resultados.Add(new ValidationResult(
                    "Si especifica estaciones para inspección, debe indicar al menos un día de viáticos.",
                    new[] { nameof(Dias) }));
            }

            // Validación personalizada: El código de solicitud debe tener un formato específico si se proporciona
            if (CodigoSolicitud.HasValue && CodigoSolicitud.Value > 0)
            {
                if (CodigoSolicitud.Value.ToString().Length < 5)
                {
                    resultados.Add(new ValidationResult(
                        "El código de solicitud debe tener al menos 5 dígitos.",
                        new[] { nameof(CodigoSolicitud) }));
                }
            }

            // Validación de Observaciones si se proporcionan
            if (!string.IsNullOrWhiteSpace(Observacion))
            {
                if (Observacion.Contains("<script>") || Observacion.Contains("javascript:"))
                {
                    resultados.Add(new ValidationResult(
                        "Las observaciones contienen contenido no permitido.",
                        new[] { nameof(Observacion) }));
                }
            }

            return resultados;
        }
    }
}