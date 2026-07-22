using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaPresentacion.Models.ViewModels
{
    public class OrdenRecaudacionPDFDetalleModel
    {
        public string ConceptoCodigo { get; set; }
        public string Concepto { get; set; }
        public int Cantidad { get; set; }
        public int NumeroDiasInspeccion { get; set; }
        public int DiasPagadosViatico { get; set; }
        public decimal ValorUnitario { get; set; }
        public string LugarInspeccion { get; set; }
        public string ProvinciaInspeccion { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Admin { get; set; }
        public decimal TotalLinea { get; set; }

        public bool EsViatico
        {
            get { return string.Equals(ConceptoCodigo, "VIATICOS_INSPECTOR", StringComparison.OrdinalIgnoreCase); }
        }
    }

    public class OrdenRecaudacionPDFModel
    {
        // Leyenda de bancos autorizados para mostrar en el PDF
        public string LeyendaBancos { get; set; }
        public string NumeroOrden { get; set; }
        public DateTime FechaEmision { get; set; }
        public string LugarEmision { get; set; } = "Quito";

        public string NombreCompania { get; set; }
        public string Ruc { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }
        public string Banco { get; set; }
        public string NumeroComprobante { get; set; }

        public string ConceptoPrincipal { get; set; }
        public decimal ValorBase { get; set; }

        public int Estaciones { get; set; }
        public decimal ValorInspecciones { get; set; }

        public int Dias { get; set; }
        public decimal ValorViaticos { get; set; }
        public decimal ValorGastosAdmin { get; set; }

        public decimal Total { get; set; }
        public string TotalEnLetras { get; set; }

        public decimal TotalSubtotal { get; set; }
        public decimal TotalAdmin { get; set; }
        public decimal TotalGeneral { get; set; }
        public List<OrdenRecaudacionPDFDetalleModel> Detalles { get; set; } = new List<OrdenRecaudacionPDFDetalleModel>();

        public string LugarInspeccion { get; set; }
        public string ProvinciaInspeccion { get; set; }
        public bool MostrarNumeroDias
        {
            get
            {
                return Detalles != null && Detalles.Any(d =>
                    d.EsViatico
                    && d.NumeroDiasInspeccion > 0
                    && Math.Abs(d.Subtotal - (Math.Max(d.NumeroDiasInspeccion - 1, 0) * d.ValorUnitario)) <= 0.01m);
            }
        }

        public string Referencia { get; set; }

        public string NombreRepresentante { get; set; }
        public string NombreInspector { get; set; }
        public string CargoInspector { get; set; }

        public void CalcularTotales()
        {
            if (Detalles != null && Detalles.Count > 0)
            {
                TotalSubtotal = Math.Round(Detalles.Sum(d => d.Subtotal), 2, MidpointRounding.AwayFromZero);
                TotalAdmin = Math.Round(Detalles.Sum(d => d.Admin), 2, MidpointRounding.AwayFromZero);
                TotalGeneral = Math.Round(Detalles.Sum(d => d.TotalLinea), 2, MidpointRounding.AwayFromZero);

                ValorBase = TotalSubtotal;
                ValorGastosAdmin = TotalAdmin;
                Total = TotalGeneral;
                TotalEnLetras = TotalGeneral.ToString("N2");
                return;
            }

            // Fallback legacy si no hay detalle cargado.
            ValorInspecciones = Estaciones * 500m;
            ValorViaticos = Dias * 80m;
            ValorGastosAdmin = ValorViaticos * 0.08m;
            Total = ValorBase + ValorInspecciones + ValorViaticos + ValorGastosAdmin;
            TotalSubtotal = ValorBase + ValorInspecciones + ValorViaticos;
            TotalAdmin = ValorGastosAdmin;
            TotalGeneral = Total;
            TotalEnLetras = Total.ToString("N2");
        }
    }

    public class SolicitudInspeccionPdfViewModel
    {
        public int OrdenId { get; set; }
        public int? SolicitudId { get; set; }
        public string NombreRT { get; set; }
        public string NombreCompania { get; set; }
        public string AeropuertosSolicitados { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public string LugarEmision { get; set; }
        public string CorreoRT { get; set; }
        public string TelefonoRT { get; set; }
        public string RucCedula { get; set; }
        public string CodigoConcepto { get; set; }
        public string NumeroOrden { get; set; }
        public string TextoResolucion { get; set; }
        public string FechasInspeccion { get; set; }
    }
}
