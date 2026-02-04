using System;
using PagoEntity = CapaDatos.Entidades.Pago;
using PagoModel = CapaModelo.PagoModel;

namespace CapaDatos.Infrastructure
{
    /// <summary>
    /// Mapper para convertir entre PagoModel (CapaModelo) y Pago (CapaDatos.Entidades)
    /// Resuelve la duplicación de modelos identificada en la auditoría de código
    /// </summary>
    public static class PagoMapper
    {
        /// <summary>
        /// Convierte una entidad Pago (CapaDatos) a PagoModel (CapaModelo)
        /// </summary>
        /// <param name="entity">Entidad Pago</param>
        /// <returns>PagoModel</returns>
        public static PagoModel ToModel(PagoEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            return new PagoModel
            {
                CodigoPago = entity.Id,
                CodigoSolicitud = entity.CodigoSolicitud,
                NumeroFactura = entity.NumeroComprobante,
                Monto = entity.MontoPagado,
                Moneda = "USD", // Default - la entidad Pago no tiene este campo
                Concepto = null, // La entidad Pago no tiene este campo
                MetodoPago = entity.MetodoPago,
                Estado = entity.Estado,
                FechaPago = entity.FechaPago == DateTime.MinValue ? (DateTime?)null : entity.FechaPago,
                FechaValidacion = entity.FechaValidacion,
                ValidadoPor = entity.UsuarioValidacion,
                Observaciones = entity.Observaciones,
                ComprobanteRuta = entity.RutaComprobante
            };
        }

        /// <summary>
        /// Convierte un PagoModel (CapaModelo) a entidad Pago (CapaDatos)
        /// </summary>
        /// <param name="model">PagoModel</param>
        /// <returns>Entidad Pago</returns>
        public static PagoEntity ToEntity(PagoModel model)
        {
            if (model == null)
            {
                return null;
            }

            return new PagoEntity
            {
                Id = model.CodigoPago,
                CodigoSolicitud = model.CodigoSolicitud,
                NumeroComprobante = model.NumeroFactura,
                MontoPagado = model.Monto,
                MetodoPago = model.MetodoPago,
                Estado = model.Estado,
                FechaPago = model.FechaPago ?? DateTime.Now,
                FechaValidacion = model.FechaValidacion,
                UsuarioValidacion = model.ValidadoPor,
                Observaciones = model.Observaciones,
                RutaComprobante = model.ComprobanteRuta,
                FechaRegistro = DateTime.Now,
                BancoOrigen = null // El modelo no tiene este campo
            };
        }

        /// <summary>
        /// Actualiza una entidad Pago existente con los valores de un PagoModel
        /// Útil para operaciones de actualización que preservan Id y fechas
        /// </summary>
        /// <param name="entity">Entidad Pago existente</param>
        /// <param name="model">PagoModel con nuevos valores</param>
        public static void UpdateEntity(PagoEntity entity, PagoModel model)
        {
            if (entity == null || model == null)
            {
                return;
            }

            // No actualizar Id ni CodigoSolicitud (son claves)
            entity.NumeroComprobante = model.NumeroFactura;
            entity.MontoPagado = model.Monto;
            entity.MetodoPago = model.MetodoPago;
            entity.Estado = model.Estado;
            
            if (model.FechaPago.HasValue)
            {
                entity.FechaPago = model.FechaPago.Value;
            }

            entity.FechaValidacion = model.FechaValidacion;
            entity.UsuarioValidacion = model.ValidadoPor;
            entity.Observaciones = model.Observaciones;
            entity.RutaComprobante = model.ComprobanteRuta;
        }

        /// <summary>
        /// Actualiza un PagoModel existente con los valores de una entidad Pago
        /// Útil para sincronizar después de operaciones de base de datos
        /// </summary>
        /// <param name="model">PagoModel existente</param>
        /// <param name="entity">Entidad Pago con nuevos valores</param>
        public static void UpdateModel(PagoModel model, Pago entity)
        {
            if (model == null || entity == null)
            {
                return;
            }

            model.CodigoPago = entity.Id;
            model.CodigoSolicitud = entity.CodigoSolicitud;
            model.NumeroFactura = entity.NumeroComprobante;
            model.Monto = entity.MontoPagado;
            model.MetodoPago = entity.MetodoPago;
            model.Estado = entity.Estado;
            model.FechaPago = entity.FechaPago == DateTime.MinValue ? (DateTime?)null : entity.FechaPago;
            model.FechaValidacion = entity.FechaValidacion;
            model.ValidadoPor = entity.UsuarioValidacion;
            model.Observaciones = entity.Observaciones;
            model.ComprobanteRuta = entity.RutaComprobante;
        }
    }
}
