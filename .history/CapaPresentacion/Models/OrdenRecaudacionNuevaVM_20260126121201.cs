using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CapaPresentacion.Models.Validation;


namespace CapaPresentacion.ViewModels
{
    public class OrdenRecaudacionNuevaVM
    {
        [Required(ErrorMessage = "RUC/Cédula es obligatorio")]
        [RucCedulaValidation(ErrorMessage = "RUC/Cédula inválido (10 o 13 dígitos válidos)")]
        public string RucCedula { get; set; }

        // Propiedad Orden que contiene los datos de la nueva orden
        public NuevaOrdenViewModel Orden { get; set; } = new NuevaOrdenViewModel();

        // Para almacenar detalles en JSON
        public string DetallesJson { get; set; }

        // Clase anidada para la nueva orden
        public class NuevaOrdenViewModel
        {
            [Required(ErrorMessage = "La compañía es requerida")]
            [StringLength(100, ErrorMessage = "Máximo 100 caracteres")]
            [Display(Name = "Compañía/Razón Social")]
            public string Compania { get; set; }

            [Required(ErrorMessage = "El RUC/Cédula es requerido")]
            [StringLength(13, MinimumLength = 10, ErrorMessage = "Entre 10 y 13 caracteres")]
            [Display(Name = "RUC/Cédula")]
            [RucCedulaValidation(ErrorMessage = "RUC/Cédula inválido")]
            public string RucCedula { get; set; }

            [Required(ErrorMessage = "El nombre del contribuyente es requerido")]
            [StringLength(200, ErrorMessage = "Máximo 200 caracteres")]
            [Display(Name = "Nombre Contribuyente")]
            public string NombreContribuyente { get; set; }

            [Required(ErrorMessage = "El correo es requerido")]
            [EmailAddress(ErrorMessage = "Formato de correo inválido")]
            [StringLength(100, ErrorMessage = "Máximo 100 caracteres")]
            [Display(Name = "Correo Electrónico")]
            public string Correo { get; set; }

            [Phone(ErrorMessage = "Formato de teléfono inválido")]
            [StringLength(15, ErrorMessage = "Máximo 15 caracteres")]
            [Display(Name = "Teléfono")]
            public string Telefono { get; set; }

            [StringLength(500, ErrorMessage = "Máximo 500 caracteres")]
            [Display(Name = "Observaciones")]
            public string Observacion { get; set; }

            // Para selección de concepto
            [Required(ErrorMessage = "Debe seleccionar al menos un concepto")]
            [Display(Name = "Conceptos")]
            public List<int> ConceptosSeleccionados { get; set; } = new List<int>();

            // Propiedades para UI
            public List<ConceptoItemViewModel> ConceptosDisponibles { get; set; } = new List<ConceptoItemViewModel>();

            // Totales (se calculan automáticamente)
            [Display(Name = "Subtotal")]
            [DisplayFormat(DataFormatString = "{0:C}")]
            public decimal Subtotal { get; set; }

            [Display(Name = "Administración")]
            [DisplayFormat(DataFormatString = "{0:C}")]
            public decimal Admin { get; set; }

            [Display(Name = "Total")]
            [DisplayFormat(DataFormatString = "{0:C}")]
            public decimal Total { get; set; }
        }

        public class ConceptoItemViewModel
        {
            public int Id { get; set; }
            public string Codigo { get; set; }
            public string Nombre { get; set; }
            public string Descripcion { get; set; }
            public decimal ValorBase { get; set; }
            public decimal PorcentajeAdmin { get; set; }
            public bool EsObligatorio { get; set; }
            public bool RequiereAprobacion { get; set; }
        }

        public class OrdenFiltroViewModel
        {
            [Display(Name = "Estado")]
            public string Estado { get; set; }

            [Display(Name = "Fecha Desde")]
            [DataType(DataType.Date)]
            [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}")]
            public DateTime? FechaDesde { get; set; }

            [Display(Name = "Fecha Hasta")]
            [DataType(DataType.Date)]
            [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}")]
            public DateTime? FechaHasta { get; set; }

            [StringLength(50, ErrorMessage = "Máximo 50 caracteres")]
            [Display(Name = "Número Orden/RUC")]
            public string Busqueda { get; set; }
        }
    }
}