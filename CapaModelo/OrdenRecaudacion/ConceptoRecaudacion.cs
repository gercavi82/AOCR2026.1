using System;

namespace CapaModelo.OrdenRecaudacion
{
    public class ConceptoRecaudacion
    {
        public int Id { get; set; }
        public string Codigo { get; set; }             // aocr_or_concepto.codigo
        public string Nombre { get; set; }             // aocr_or_concepto.nombre
        public string TipoCalculo { get; set; }        // FIJO, POR_ESTACION, POR_DIA_MAS_PORC, etc.
        public decimal ValorBase { get; set; }         // valor_base
        public decimal PorcentajeAdmin { get; set; }   // porcentaje_admin (ej 0.08 o 8 según tu data)
        public bool Activo { get; set; }
        public int Orden { get; set; }
        public string Descripcion { get; set; }
        public bool PorEstacion { get; set; }
        public bool PorDia { get; set; }
        public bool? EsViatico { get; set; }
    }
}
