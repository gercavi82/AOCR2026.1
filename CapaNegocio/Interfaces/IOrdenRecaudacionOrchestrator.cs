using System.Threading.Tasks;
using CapaNegocio.DTOs;

namespace CapaNegocio.Interfaces
{
    /// <summary>
    /// Orquestador del flujo principal de Orden de recaudación.
    /// Coordina: Crear Orden → Registrar Pago → Validar Pago → Generar PDF → Notificar
    /// </summary>
    public interface IOrdenRecaudacionOrchestrator
    {
        #region Operaciones Principales del Flujo

        /// <summary>
        /// Paso 1: Crear una nueva orden de recaudación
        /// </summary>
        Task<OperationResult<CrearOrdenResponse>> CrearOrdenAsync(CrearOrdenRequest request);

        /// <summary>
        /// Paso 2: Registrar pago con comprobante
        /// </summary>
        Task<OperationResult<RegistrarPagoResponse>> RegistrarPagoAsync(RegistrarPagoRequest request);

        /// <summary>
        /// Paso 3: Validar pago (aprobación/rechazo por financiero)
        /// </summary>
        Task<OperationResult<ValidarPagoResponse>> ValidarPagoAsync(ValidarPagoRequest request);

        /// <summary>
        /// Paso 4: Generar PDF de orden o factura
        /// </summary>
        Task<OperationResult<GenerarPdfResponse>> GenerarPdfAsync(GenerarPdfRequest request);

        /// <summary>
        /// Paso 5: Enviar notificación por correo
        /// </summary>
        Task<OperationResult<EnviarNotificacionResponse>> EnviarNotificacionAsync(EnviarNotificacionRequest request);

        #endregion

        #region Operaciones de Consulta

        /// <summary>
        /// Obtener estado actual del flujo de una orden
        /// </summary>
        Task<OperationResult<FlujoOrdenCompleto>> ObtenerEstadoFlujoAsync(int ordenId);

        /// <summary>
        /// Verificar si una orden puede avanzar al siguiente paso
        /// </summary>
        Task<OperationResult<bool>> PuedeAvanzarAsync(int ordenId, string siguientePaso);

        #endregion

        #region Validaciones

        /// <summary>
        /// Validar datos de creación de orden
        /// </summary>
        OperationResult ValidarCrearOrden(CrearOrdenRequest request);

        /// <summary>
        /// Validar datos de registro de pago
        /// </summary>
        OperationResult ValidarRegistrarPago(RegistrarPagoRequest request);

        /// <summary>
        /// Validar archivo de comprobante
        /// </summary>
        OperationResult ValidarArchivoComprobante(string nombreArchivo, string tipoArchivo, long tamanoBytes);

        #endregion
    }
}
