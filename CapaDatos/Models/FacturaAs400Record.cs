using System;
using System.Collections.Generic;

namespace CapaDatos.Models
{
    public class FacturaAs400Record
    {
        public int OrdenId { get; set; }
        public int? PagoId { get; set; }
        public string NumeroFactura { get; set; }
        public string AutorizacionFactura { get; set; }
        public DateTime FechaEmision { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public string Observaciones { get; set; }

        // Datos de contribuyente
        public string Ruc { get; set; }
        public string Correo { get; set; }
        public string Compania { get; set; }
        public string Telefono { get; set; }

        // Datos base FR3/OPCAR
        public string Aeropuerto { get; set; }
        public string Anio { get; set; }
        public string FechaControl { get; set; }
        public string TipoOperacion { get; set; }
        public string Ruta { get; set; }
        public int NumAterrizaPais { get; set; }
        public string FormaPago { get; set; }
        public string CodigoBanco { get; set; }
        public string Deposito { get; set; }
        public string UsuarioRegistro { get; set; }

        // Campos opcionales FR3 (compatibilidad con OPCAR5)
        public string Autorizacion { get; set; }
        public string GranTotalLetras { get; set; }
        public string NacInter { get; set; }
        public string NombreAeropuerto { get; set; }
        public string EmailUsuarioDGAC { get; set; }
        public string Matricula { get; set; }
        public string Callsign { get; set; }
        public string Modelo { get; set; }
        public decimal? PesoMatricula { get; set; }
        public string CodigoOACICia { get; set; }
        public string FechaRecepcion { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string Retorno { get; set; }
        public decimal? OidCiaAviacion { get; set; }
        public decimal? OidUbicacion { get; set; }
        public decimal? OidUbicacionCliente { get; set; }
        public decimal? IdAeropuerto { get; set; }
        public string Procesado { get; set; }

        public List<FacturaAs400Detalle> Detalles { get; set; } = new List<FacturaAs400Detalle>();
    }

    public class FacturaAs400Detalle
    {
        public string CodigoContable { get; set; }
        public string Descripcion { get; set; }
        public decimal Cantidad { get; set; }
        public decimal Valor { get; set; }
        public decimal Total { get; set; }
        public string TipoCobro { get; set; }
        public decimal? OidFormulario { get; set; }
        public string HacerDescuento { get; set; }
        public string CobrarImpuesto { get; set; }
        public string IngresarCantidad { get; set; }
        public string DescripcionCuenta { get; set; }
        public string Codigo { get; set; }
    }
}
