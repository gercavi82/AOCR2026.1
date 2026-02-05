using System;
using CapaDatos.DAOs;


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

        /// <summary>
        /// Calcula totales usando parámetros configurables desde la base de datos
        /// Elimina valores hardcodeados ($500, $80, 8%)
        /// </summary>
        public void CalcularTotales()
        {
            try
            {
                var parametroDAO = new ParametroDAO();
                var parametrosCalculo = parametroDAO.ObtenerParametrosCalculoOrden();

                var valorPorEstacion = parametrosCalculo["CALCULO_VALOR_POR_ESTACION"];
                var valorPorDiaViatico = parametrosCalculo["CALCULO_VALOR_POR_DIA_VIATICO"];
                var porcentajeGastosAdmin = parametrosCalculo["CALCULO_PORCENTAJE_GASTOS_ADMIN"];

                ValorInspecciones = Estaciones * valorPorEstacion;
                ValorViaticos = Dias * valorPorDiaViatico;
                ValorGastosAdmin = ValorViaticos * (porcentajeGastosAdmin / 100m);
                Total = ValorBase + ValorInspecciones + ValorViaticos + ValorGastosAdmin;
                TotalEnLetras = Total.ToString("N2");
            }
            catch (Exception ex)
            {
                // Si falla la configuración dinámica, usar valores por defecto
                // pero registrar el error para diagnóstico
                System.Diagnostics.Debug.WriteLine($"Error obteniendo parámetros de cálculo: {ex.Message}");
                
                // Valores por defecto (los mismos que antes estaban hardcodeados)
                ValorInspecciones = Estaciones * 500m;
                ValorViaticos = Dias * 80m;
                ValorGastosAdmin = ValorViaticos * 0.08m;
                Total = ValorBase + ValorInspecciones + ValorViaticos + ValorGastosAdmin;
                TotalEnLetras = Total.ToString("N2");
            }
        }
    }
}
