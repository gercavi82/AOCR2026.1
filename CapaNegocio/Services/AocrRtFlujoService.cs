using System;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.Services;

namespace CapaNegocio.Services
{
    public class AocrRtFlujoService
    {
        private readonly ILoggingService _logger = LoggingServiceFactory.Create();
        private readonly AocrProcesoActivoService _procesoActivoService = new AocrProcesoActivoService();
        private readonly AocrCompaniaContextService _companiaContext = new AocrCompaniaContextService();
        private readonly ComprobanteService _comprobanteService = new ComprobanteService();

        public AocrRtFlujoEstadoViewModel ObtenerEstadoFlujoRt(int usuarioId, string companiaCodigo, string companiaNombre = null)
        {
            var vm = new AocrRtFlujoEstadoViewModel
            {
                UsuarioId = usuarioId,
                CompaniaCodigo = companiaCodigo ?? string.Empty,
                CompaniaNombre = companiaNombre ?? string.Empty,
                TieneCompaniaActiva = usuarioId > 0 && !string.IsNullOrWhiteSpace(companiaCodigo)
            };

            _logger.LogInfo(string.Format("[RT_FLUJO][RESOLVE_IN] UsuarioId={0}; CompaniaCodigo={1};", usuarioId, companiaCodigo ?? "N/A"));

            if (!vm.TieneCompaniaActiva)
            {
                vm.SiguientePaso = "SELECCIONAR_COMPANIA";
                vm.UrlDestino = "/Account/SeleccionarCompania";
                vm.MensajeUsuario = "Debe seleccionar una compañía activa para continuar.";
                return vm;
            }

            var procesoInfo = _procesoActivoService.ObtenerProcesoActivoPorCompania(usuarioId, companiaCodigo, companiaNombre);

            if (procesoInfo.SolicitudActiva != null && procesoInfo.SolicitudActiva.CodigoSolicitud > 0)
            {
                vm.SolicitudAocrCreada = true;
                vm.SolicitudAocrId = procesoInfo.SolicitudActiva.CodigoSolicitud;
                vm.EstadoSolicitudAocr = procesoInfo.SolicitudActiva.Estado;
            }

            if (procesoInfo.OrdenActiva != null && procesoInfo.OrdenActiva.Id > 0)
            {
                var o = procesoInfo.OrdenActiva;
                var estadoNorm = EstadoOrden.NormalizarEstado(o.Estado);

                if (estadoNorm != EstadoOrden.Anulada)
                {
                    vm.TieneOrdenVigente = true;
                    vm.OrdenRecaudacionId = o.Id;
                    vm.NumeroOrden = o.NumeroOrden;
                    vm.EstadoOrden = o.Estado;

                    vm.TieneComprobante = _comprobanteService.ExisteComprobanteValido(o.Id, out _);
                    vm.PagoAprobado = EstadoOrden.EsPagado(o.Estado) || EstadoOrden.EsOrdenCerradaPostAprobacionFinanciera(o.Estado);
                    vm.Fr3Vinculado = string.Equals(estadoNorm, "FR3_VINCULADO", StringComparison.OrdinalIgnoreCase);

                    if (vm.PagoAprobado || vm.Fr3Vinculado)
                    {
                        vm.SolicitudAocrHabilitada = true;
                    }
                }
            }

            // Si no hay orden vigente ni activa
            if (!vm.TieneOrdenVigente)
            {
                _logger.LogInfo(string.Format("[RT_FLUJO][SIN_ORDEN] UsuarioId={0}; CompaniaCodigo={1}; Destino=OrdenRecaudacion/Nueva;", usuarioId, companiaCodigo));
                vm.SiguientePaso = "GENERAR_ORDEN_RECAUDACION";
                vm.UrlDestino = "/OrdenRecaudacion/Nueva";
                vm.MensajeUsuario = "Debe generar la Orden de Recaudación para continuar.";
                return vm;
            }

            // Si pago está aprobado pero aún no ha abierto/guardado la solicitud AOCR (o si la solicitud está habilitada)
            if (vm.PagoAprobado || vm.SolicitudAocrHabilitada)
            {
                // Si hay solicitud AOCR ya creada, continuarla
                if (vm.SolicitudAocrCreada)
                {
                    _logger.LogInfo(string.Format("[RT_FLUJO][SOLICITUD_EXISTENTE] UsuarioId={0}; CompaniaCodigo={1}; SolicitudId={2}; Estado={3};",
                        usuarioId, companiaCodigo, vm.SolicitudAocrId, vm.EstadoSolicitudAocr));

                    if (EstadoSolicitud.PermiteEdicionFormularioEmision(vm.EstadoSolicitudAocr))
                    {
                        vm.SiguientePaso = "CONTINUAR_SOLICITUD_AOCR";
                        vm.UrlDestino = "/SolicitudAOCR/FormularioEmisionAOCR?oid=" + vm.SolicitudAocrId;
                        vm.MensajeUsuario = "Continúe con su Solicitud AOCR.";
                    }
                    else
                    {
                        vm.SiguientePaso = "VER_SEGUIMIENTO_SOLICITUD";
                        vm.UrlDestino = "/SolicitudAOCR/Detalle/" + vm.SolicitudAocrId;
                        vm.MensajeUsuario = "Su Solicitud AOCR se encuentra en proceso.";
                    }
                    return vm;
                }

                _logger.LogInfo(string.Format("[RT_FLUJO][PAGO_APROBADO] UsuarioId={0}; CompaniaCodigo={1}; OrdenId={2}; Destino=SolicitudAOCR;",
                    usuarioId, companiaCodigo, vm.OrdenRecaudacionId));

                vm.SolicitudAocrHabilitada = true;
                vm.SiguientePaso = "ABRIR_SOLICITUD_AOCR";
                vm.UrlDestino = "/SolicitudAOCR/FormularioEmisionAOCR";
                vm.MensajeUsuario = "Su Solicitud AOCR está habilitada.";
                return vm;
            }

            // Si hay orden generada pero sin comprobante
            if (!vm.TieneComprobante)
            {
                _logger.LogInfo(string.Format("[RT_FLUJO][ORDEN_PENDIENTE_PAGO] UsuarioId={0}; CompaniaCodigo={1}; OrdenId={2}; EstadoOrden={3};",
                    usuarioId, companiaCodigo, vm.OrdenRecaudacionId, vm.EstadoOrden));

                vm.SiguientePaso = "SUBIR_COMPROBANTE";
                vm.UrlDestino = "/OrdenRecaudacion/Index"; // Cambiado a Index ya que Detalles no existe
                vm.MensajeUsuario = "Debe registrar el comprobante de pago para continuar.";
                return vm;
            }

            // Si tiene comprobante cargado pero Financiero no aprueba aún
            _logger.LogInfo(string.Format("[RT_FLUJO][ORDEN_PENDIENTE_PAGO] UsuarioId={0}; CompaniaCodigo={1}; OrdenId={2}; EstadoOrden={3};",
                usuarioId, companiaCodigo, vm.OrdenRecaudacionId, vm.EstadoOrden));

            vm.SiguientePaso = "ESPERAR_APROBACION_FINANCIERO";
            vm.UrlDestino = "/Dashboard/Index";
            vm.MensajeUsuario = "Su pago está pendiente de revisión por Financiero.";
            return vm;
        }
    }
}
