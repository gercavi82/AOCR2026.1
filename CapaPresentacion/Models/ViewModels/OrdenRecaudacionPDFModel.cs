using System;


namespace CapaPresentacion.Models.ViewModels
{
    public class OrdenRecaudacionPDFModel
    {
        public string NumeroOrden { get; set; }
        public DateTime FechaEmision { get; set; }
        public string LugarEmision { get; set; } = "Quito";

        public string NombreCompania { get; set; }
        public string Ruc { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }

        public string ConceptoPrincipal { get; set; }
        public decimal ValorBase { get; set; }

        public int Estaciones { get; set; }
        public decimal ValorInspecciones { get; set; }

        public int Dias { get; set; }
        public decimal ValorViaticos { get; set; }
        public decimal ValorGastosAdmin { get; set; }

        public decimal Total { get; set; }
        public string TotalEnLetras { get; set; }

        public string Referencia { get; set; }

        public string NombreRepresentante { get; set; }
        public string NombreInspector { get; set; }
        public string CargoInspector { get; set; }

        public void CalcularTotales()

        
        {
            ValorInspecciones = Estaciones * 500m;
            ValorViaticos = Dias * 80m;
            ValorGastosAdmin = ValorViaticos * 0.08m;
            Total = ValorBase + ValorInspecciones + ValorViaticos + ValorGastosAdmin;
            TotalEnLetras = Total.ToString("N2");
        }
    }
}
