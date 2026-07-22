using System;
using CapaDatos.Services;

namespace CapaNegocio.Services
{
    public class AuditoriaService
    {
        private readonly AuditTrailService _auditTrailService;

        public AuditoriaService()
        {
            _auditTrailService = new AuditTrailService();
        }

        public void RegistrarEvento(
            string modulo,
            string accion,
            string entidad,
            int? entidadId,
            string estadoAnterior,
            string estadoNuevo,
            int? usuarioId,
            string usuarioNombre,
            string observacion,
            string ip,
            string datosResumen)
        {
            _auditTrailService.RegistrarAuditoria(
                tabla: entidad,
                registroId: entidadId,
                accion: accion,
                campoModificado: "estado",
                valorAnterior: estadoAnterior,
                valorNuevo: estadoNuevo,
                usuarioId: usuarioId,
                usuarioNombre: usuarioNombre,
                ipOrigen: ip,
                modulo: modulo,
                metadata: datosResumen ?? observacion);
        }

        public void RegistrarCambioEstadoInspeccion(
            int codigoInspeccion,
            string estadoAnterior,
            string estadoNuevo,
            int? usuarioId,
            string usuarioNombre,
            string observacion,
            string ip,
            string datosResumen)
        {
            RegistrarEvento(
                modulo: "Inspeccion",
                accion: "CAMBIO_ESTADO",
                entidad: "aocr_tbinspeccion",
                entidadId: codigoInspeccion,
                estadoAnterior: estadoAnterior,
                estadoNuevo: estadoNuevo,
                usuarioId: usuarioId,
                usuarioNombre: usuarioNombre,
                observacion: observacion,
                ip: ip,
                datosResumen: datosResumen);
        }

        public void RegistrarAccionInspeccion(
            int codigoInspeccion,
            string accion,
            int? usuarioId,
            string usuarioNombre,
            string observacion,
            string ip,
            string datosResumen)
        {
            RegistrarEvento(
                modulo: "Inspeccion",
                accion: accion,
                entidad: "aocr_tbinspeccion",
                entidadId: codigoInspeccion,
                estadoAnterior: null,
                estadoNuevo: null,
                usuarioId: usuarioId,
                usuarioNombre: usuarioNombre,
                observacion: observacion,
                ip: ip,
                datosResumen: datosResumen);
        }

        public void RegistrarGeneracionNumeroOrden(
            int ordenId,
            string numeroOrden,
            int? usuarioId,
            string usuarioNombre,
            string companiaCodigo,
            string ip)
        {
            RegistrarEvento(
                modulo: "OrdenRecaudacion",
                accion: "ORDEN_GENERADA",
                entidad: "aocr_or_orden",
                entidadId: ordenId,
                estadoAnterior: null,
                estadoNuevo: "GENERADA",
                usuarioId: usuarioId,
                usuarioNombre: usuarioNombre,
                observacion: $"NumeroOrden={numeroOrden}; CompaniaCodigo={companiaCodigo}",
                ip: ip,
                datosResumen: $"Generación de número de orden inmutable {numeroOrden}");
        }
    }
}
