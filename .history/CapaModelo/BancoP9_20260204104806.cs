namespace CapaModelo
{
    /// <summary>
    /// Entidad para representar los bancos obtenidos desde P9
    /// </summary>
    public class BancoP9
    {
        /// <summary>
        /// Código del banco (VALVAL)
        /// </summary>
        public string Codigo { get; set; }

        /// <summary>
        /// Descripción/Nombre del banco (VALDES)
        /// </summary>
        public string Descripcion { get; set; }
    }
}