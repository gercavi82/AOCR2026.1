using System.ComponentModel.DataAnnotations;

namespace CapaDatos.Models
{
    public class ConceptoModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El código es requerido")]
        [StringLength(30)]
        public string Codigo { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(200)]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El tipo de cálculo es requerido")]
        [StringLength(30)]
        public string TipoCalculo { get; set; } // FIJO, VARIABLE, PORCENTAJE

        [Required(ErrorMessage = "El valor base es requerido")]
        [Range(0, double.MaxValue, ErrorMessage = "El valor base debe ser mayor o igual a 0")]
        public decimal ValorBase { get; set; }

        [Required(ErrorMessage = "El porcentaje de administración es requerido")]
        [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100")]
        public decimal PorcentajeAdmin { get; set; }

        public bool Activo { get; set; } = true;
        public int Orden { get; set; } = 1;

        public string Descripcion { get; set; }
        public bool PorEstacion { get; set; } = false;
        public bool PorDia { get; set; } = false;
        public bool EsViatico { get; set; } = false;

        // Propiedades calculadas
        public string NombreCompleto => $"{Codigo} - {Nombre}";

        public decimal CalcularTotal(decimal cantidad = 1, int? estaciones = null, int? dias = null)
        {
            decimal baseCalculo = ValorBase;

            if (PorEstacion && estaciones.HasValue)
                baseCalculo = ValorBase * estaciones.Value;

            if (PorDia && dias.HasValue)
                baseCalculo = ValorBase * dias.Value;

            var admin = baseCalculo * (PorcentajeAdmin / 100);
            return baseCalculo + admin;
        }
    }
}