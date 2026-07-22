using System;
using System.Collections.Generic;

namespace CapaNegocio.DTOs
{
    /// <summary>
    /// DTO para crear una orden de recaudación
    /// </summary>
    public class CrearOrdenRequest
    {
        public int SolicitudId { get; set; }
        public int ConceptoId { get; set; }
        public int ContribuyenteId { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public string Observaciones { get; set; }
        public string UsuarioCreacion { get; set; }
        public List<DetalleOrdenRequest> Detalles { get; set; }

        public CrearOrdenRequest()
        {
            Detalles = new List<DetalleOrdenRequest>();
        }
    }

    /// <summary>
    /// DTO para detalle de orden
    /// </summary>
    public class DetalleOrdenRequest
    {
        public int ConceptoId { get; set; }
        public string Descripcion { get; set; }
        public int Cantidad { get; set; }
        public int? NumeroDiasInspeccion { get; set; }
        public int? DiasPagadosViatico { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        
        public string LugarInspeccion { get; set; }
        public string ProvinciaInspeccion { get; set; }
    }

    /// <summary>
    /// DTO para respuesta de creación de orden
    /// </summary>
    public class CrearOrdenResponse
    {
        public int OrdenId { get; set; }
        public string NumeroOrden { get; set; }
        public string Estado { get; set; }
        public DateTime FechaCreacion { get; set; }
        public decimal Total { get; set; }
    }

    /// <summary>
    /// DTO para registrar un pago
    /// </summary>
    public class RegistrarPagoRequest
    {
        public int OrdenId { get; set; }
        public string NumeroComprobante { get; set; }
        public decimal MontoPagado { get; set; }
        public DateTime FechaPago { get; set; }
        public string MetodoPago { get; set; }
        public string BancoOrigen { get; set; }
        public string Observaciones { get; set; }
        public string UsuarioRegistro { get; set; }
        
        // Información del archivo comprobante
        public string NombreArchivo { get; set; }
        public string TipoArchivo { get; set; }
        public byte[] ContenidoArchivo { get; set; }
        public long TamanoArchivo { get; set; }
    }

    /// <summary>
    /// DTO para respuesta de registro de pago
    /// </summary>
    public class RegistrarPagoResponse
    {
        public int PagoId { get; set; }
        public int OrdenId { get; set; }
        public string NumeroComprobante { get; set; }
        public string EstadoOrden { get; set; }
        public string RutaComprobante { get; set; }
    }

    /// <summary>
    /// DTO para validación de pago (financiero)
    /// </summary>
    public class ValidarPagoRequest
    {
        public int PagoId { get; set; }
        public bool Aprobado { get; set; }
        public string Observaciones { get; set; }
        public string UsuarioValidacion { get; set; }
    }

    /// <summary>
    /// DTO para respuesta de validación
    /// </summary>
    public class ValidarPagoResponse
    {
        public int PagoId { get; set; }
        public int OrdenId { get; set; }
        public string EstadoPago { get; set; }
        public string EstadoOrden { get; set; }
        public bool FacturaGenerada { get; set; }
        public string NumeroFactura { get; set; }
        public bool NotificacionEnviada { get; set; }
    }

    /// <summary>
    /// DTO para generación de PDF
    /// </summary>
    public class GenerarPdfRequest
    {
        public int OrdenId { get; set; }
        public string TipoDocumento { get; set; } // "ORDEN", "FACTURA", "COMPROBANTE"
        public bool IncluirDetalles { get; set; }
        public bool IncluirFirmaDigital { get; set; }
    }

    /// <summary>
    /// DTO para respuesta de generación de PDF
    /// </summary>
    public class GenerarPdfResponse
    {
        public byte[] ContenidoPdf { get; set; }
        public string NombreArchivo { get; set; }
        public string ContentType { get; set; }
        public long TamanoBytes { get; set; }
    }

    /// <summary>
    /// DTO para respuesta de notificación
    /// </summary>
    public class EnviarNotificacionResponse
    {
        public bool Enviado { get; set; }
        public string MessageId { get; set; }
        public DateTime? FechaEnvio { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// DTO para flujo completo de orden
    /// </summary>
    public class FlujoOrdenCompleto
    {
        public CrearOrdenResponse Orden { get; set; }
        public RegistrarPagoResponse Pago { get; set; }
        public ValidarPagoResponse Validacion { get; set; }
        public GenerarPdfResponse Pdf { get; set; }
        public EnviarNotificacionResponse Notificacion { get; set; }
        public List<string> PasosCompletados { get; set; }
        public string EstadoActual { get; set; }

        public FlujoOrdenCompleto()
        {
            PasosCompletados = new List<string>();
        }
    }
}
