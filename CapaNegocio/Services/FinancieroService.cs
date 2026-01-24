using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class FinancieroService
    {
        private readonly PagoDAO _pagoDAO;
        private readonly SolicitudDAO _solicitudDAO;

        public FinancieroService()
        {
            _pagoDAO = new PagoDAO();
            _solicitudDAO = new SolicitudDAO();
        }

        // 1. Registrar pago con validación de existencia
        public ResultadoOperacion RegistrarPago(Pago pago)
        {
            try
            {
                if (pago == null)
                    return ResultadoOperacion.Error("El pago no puede ser nulo.");

                var solicitud = _solicitudDAO.ObtenerPorId(pago.CodigoSolicitud);
                if (solicitud == null)
                    return ResultadoOperacion.Error("La solicitud técnica no existe en el sistema.");

                // Valores de auditoría por defecto
                if (pago.FechaPago == DateTime.MinValue)
                    pago.FechaPago = DateTime.Now;

                pago.Estado = "REGISTRADO";
                pago.NumeroTransaccion = GenerarNumeroTransaccion();

                int pagoId = _pagoDAO.Crear(pago);

                if (pagoId > 0)
                {
                    return ResultadoOperacion.Ok(new
                    {
                        PagoId = pagoId,
                        NumeroTransaccion = pago.NumeroTransaccion
                    }, "Pago registrado exitosamente para revisión financiera.");
                }

                return ResultadoOperacion.Error("No se pudo persistir el registro del pago.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error interno en el servicio de registro: " + ex.Message);
            }
        }

        // 2. Validar pago y disparar flujo técnico
        public ResultadoOperacion ValidarPagoInicial(int codigoSolicitud)
        {
            try
            {
                var pago = _pagoDAO.ObtenerPorSolicitud(codigoSolicitud);
                if (pago == null)
                    return ResultadoOperacion.Error("No existe un comprobante de pago para esta solicitud.");

                if (pago.Estado != "REGISTRADO")
                    return ResultadoOperacion.Error("El pago no está en estado pendiente para validación.");

                // Transacción lógica: Validar Pago
                pago.Estado = "VALIDADO";
                pago.FechaValidacion = DateTime.Now;
                pago.UsuarioValidacion = ObtenerUsuarioActual();

                if (!_pagoDAO.Actualizar(pago))
                    return ResultadoOperacion.Error("Fallo crítico al actualizar el comprobante de pago.");

                // Transacción lógica: Actualizar Solicitud al Área Técnica
                var solicitud = _solicitudDAO.ObtenerPorId(codigoSolicitud);
                solicitud.Estado = "PENDIENTE_ASIGNACION_TECNICA";

                if (!_solicitudDAO.Actualizar(solicitud))
                    return ResultadoOperacion.Error("Pago validado, pero no se pudo notificar al área técnica.");

                return ResultadoOperacion.Ok(null, "Proceso financiero completado. Solicitud enviada a asignación técnica.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error en el proceso de validación: " + ex.Message);
            }
        }

        private string GenerarNumeroTransaccion()
        {
            return string.Format("TRX-{0}-{1}",
                DateTime.Now.ToString("yyyyMMddHHmmss"),
                new Random().Next(1000, 9999));
        }

        private string ObtenerUsuarioActual()
        {
            // En producción, esto debe capturar el Id del usuario de la sesión
            return "SISTEMA_FINANCIERO";
        }
    }
}