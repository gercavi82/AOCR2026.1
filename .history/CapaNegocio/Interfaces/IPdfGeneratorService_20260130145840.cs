using System.Threading.Tasks;
using CapaDatos.Entidades;

namespace CapaNegocio.Interfaces
{
    /// <summary>
    /// Interface para servicio de generación de PDF
    /// </summary>
    public interface IPdfGeneratorService
    {
        /// <summary>
        /// Genera PDF de orden de recaudación
        /// </summary>
        Task<byte[]> GenerarOrdenRecaudacionPdfAsync(OrdenRecaudacion orden);

        /// <summary>
        /// Genera PDF de factura
        /// </summary>
        Task<byte[]> GenerarFacturaPdfAsync(OrdenRecaudacion orden);

        /// <summary>
        /// Genera PDF de comprobante de pago
        /// </summary>
        Task<byte[]> GenerarComprobantePagoPdfAsync(Pago pago);
    }
}
