using System;

namespace CapaModelo
{
    public class ConceptoModel
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string TipoCalculo { get; set; }
        public decimal ValorBase { get; set; }
        public decimal PorcentajeAdmin { get; set; }
        public bool Activo { get; set; }
        public int Orden { get; set; }
        public bool PorEstacion { get; set; }
        public bool PorDia { get; set; }
        public bool EsViatico { get; set; }
    }
}
