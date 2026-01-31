namespace CapaDatos.Entidades
{
    /// <summary>
    /// Entidad que representa un detalle de orden de recaudación
    /// </summary>
    public class DetalleOrden
    {
        public int Id { get; set; }
        public int OrdenId { get; set; }
        public int ConceptoId { get; set; }
        public string Descripcion { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }

        // Navegación
        public virtual OrdenRecaudacion Orden { get; set; }
    }
}
