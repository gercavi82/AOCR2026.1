using System;

namespace CapaModelo.RT.ViewModels
{
    public class DeclaracionPdfVM
    {
        public string NombreCompleto { get; set; }
        public string Compania { get; set; }
        public string TextoDeclaracion { get; set; }
        public DateTime FechaEmision { get; set; }
    }
}
