using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CapaPresentacion.ViewModels
{
    public class ConceptoOptionVM
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public decimal Valor { get; set; }
        public decimal PorcentajeAdmin { get; set; }
        public string Label { get; set; }
    }
}