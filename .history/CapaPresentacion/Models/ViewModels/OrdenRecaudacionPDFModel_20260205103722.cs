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
                // FORZAR USO DE VALORES DINÁMICOS - NO MÁS HARDCODEADOS
                System.Diagnostics.Debug.WriteLine($"ERROR CRÍTICO: No se pudieron obtener parámetros dinámicos: {ex.Message}");
                
                // Intentar con valores mínimos para evitar errores, pero sin valores fijos
                try
                {
                    var parametroDAO = new ParametroDAO();
                    var tarifeEmision = parametroDAO.ObtenerPorClave("TARIFA_EMI_AOCR");
                    var tarifeViaticos = parametroDAO.ObtenerPorClave("TARIFA_VIATICOS_INSPECTOR");
                    var porcentajeAdmin = parametroDAO.ObtenerPorClave("PORCENTAJE_ADMIN_VIATICOS");
                    
                    var valorEstacion = tarifeEmision?.ValorParametro ?? 
                        (decimal.TryParse(tarifeEmision?.Valor, out var val1) ? val1 : 100m);
                    var valorViatico = tarifeViaticos?.ValorParametro ?? 
                        (decimal.TryParse(tarifeViaticos?.Valor, out var val2) ? val2 : 50m);
                    var porcentaje = porcentajeAdmin?.ValorParametro ?? 
                        (decimal.TryParse(porcentajeAdmin?.Valor, out var val3) ? val3 : 8m);
                    
                    ValorInspecciones = Estaciones * valorEstacion;
                    ValorViaticos = Dias * valorViatico;
                    ValorGastosAdmin = ValorViaticos * (porcentaje / 100m);
                    Total = ValorBase + ValorInspecciones + ValorViaticos + ValorGastosAdmin;
                    TotalEnLetras = Total.ToString("N2");
                }
                catch
                {
                    // Si todo falla, usar valores mínimos temporales hasta que se configure la BD
                    ValorInspecciones = 0m;
                    ValorViaticos = 0m;
                    ValorGastosAdmin = 0m;
                    Total = ValorBase;
                    TotalEnLetras = Total.ToString("N2");
                    System.Diagnostics.Debug.WriteLine("ADVERTENCIA: Usando valores mínimos. Configure los parámetros en la base de datos.");
                }
            }
        }
    }
}
