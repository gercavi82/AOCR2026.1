using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaDatos.Constants
{
    /// <summary>
    /// Tipos y niveles de notificaciones del sistema AOCR
    /// Centraliza las constantes de tipo, nivel y plantillas básicas
    /// </summary>
    public static class TiposNotificacion
    {
        // ============================================
        // NIVELES DE PRIORIDAD (Tipo visual)
        // ============================================
        
        /// <summary>Información general (azul)</summary>
        public const string INFO = "INFO";
        
        /// <summary>Operación exitosa (verde)</summary>
        public const string SUCCESS = "SUCCESS";
        
        /// <summary>Advertencia, requiere atención (amarillo)</summary>
        public const string WARNING = "WARNING";
        
        /// <summary>Error o situación crítica (rojo)</summary>
        public const string ERROR = "ERROR";


        // ============================================
        // CATEGORÍAS DE NOTIFICACIONES
        // ============================================
        
        /// <summary>Notificaciones sobre solicitudes AOCR</summary>
        public const string CATEGORIA_SOLICITUD = "SOLICITUD";
        
        /// <summary>Notificaciones sobre inspecciones</summary>
        public const string CATEGORIA_INSPECCION = "INSPECCION";
        
        /// <summary>Notificaciones sobre pagos y órdenes</summary>
        public const string CATEGORIA_PAGO = "PAGO";
        
        /// <summary>Notificaciones sobre documentos y subsanaciones</summary>
        public const string CATEGORIA_DOCUMENTO = "DOCUMENTO";
        
        /// <summary>Notificaciones sobre certificados AOCR</summary>
        public const string CATEGORIA_CERTIFICADO = "CERTIFICADO";
        
        /// <summary>Notificaciones del sistema (actualizaciones, mantenimiento)</summary>
        public const string CATEGORIA_SISTEMA = "SISTEMA";


        // ============================================
        // EVENTOS ESPECÍFICOS
        // ============================================
        
        // Solicitudes
        public const string SOLICITUD_NUEVA = "SOLICITUD_NUEVA";
        public const string SOLICITUD_CAMBIO_ESTADO = "SOLICITUD_CAMBIO_ESTADO";
        public const string SOLICITUD_APROBADA = "SOLICITUD_APROBADA";
        public const string SOLICITUD_RECHAZADA = "SOLICITUD_RECHAZADA";
        
        // Inspecciones
        public const string INSPECCION_PROGRAMADA = "INSPECCION_PROGRAMADA";
        public const string INSPECCION_COMPLETADA = "INSPECCION_COMPLETADA";
        public const string INSPECCION_APLAZADA = "INSPECCION_APLAZADA";
        
        // Pagos
        public const string PAGO_RECIBIDO = "PAGO_RECIBIDO";
        public const string PAGO_VENCIDO = "PAGO_VENCIDO";
        public const string ORDEN_GENERADA = "ORDEN_GENERADA";
        
        // Documentos
        public const string DOCUMENTO_FALTANTE = "DOCUMENTO_FALTANTE";
        public const string SUBSANACION_SOLICITADA = "SUBSANACION_SOLICITADA";
        public const string SUBSANACION_COMPLETADA = "SUBSANACION_COMPLETADA";
        
        // Certificados
        public const string CERTIFICADO_EMITIDO = "CERTIFICADO_EMITIDO";
        public const string CERTIFICADO_PROXIMO_VENCER = "CERTIFICADO_PROXIMO_VENCER";
        public const string CERTIFICADO_VENCIDO = "CERTIFICADO_VENCIDO";


        // ============================================
        // VALIDACIÓN
        // ============================================
        
        public static readonly string[] NivelesValidos = new[]
        {
            INFO, SUCCESS, WARNING, ERROR
        };

        public static readonly string[] CategoriasValidas = new[]
        {
            CATEGORIA_SOLICITUD, CATEGORIA_INSPECCION, CATEGORIA_PAGO,
            CATEGORIA_DOCUMENTO, CATEGORIA_CERTIFICADO, CATEGORIA_SISTEMA
        };

        public static bool EsNivelValido(string nivel)
        {
            if (string.IsNullOrWhiteSpace(nivel))
                return false;

            return NivelesValidos.Contains(nivel.ToUpperInvariant());
        }

        public static bool EsCategoriaValida(string categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria))
                return false;

            return CategoriasValidas.Contains(categoria.ToUpperInvariant());
        }


        // ============================================
        // COLORES PARA BADGES (Bootstrap)
        // ============================================
        
        public static string ObtenerColorBadge(string nivel)
        {
            if (string.IsNullOrWhiteSpace(nivel))
                return "default";

            switch (nivel.ToUpperInvariant())
            {
                case INFO:
                    return "info";          // Azul
                case SUCCESS:
                    return "success";       // Verde
                case WARNING:
                    return "warning";       // Amarillo
                case ERROR:
                    return "danger";        // Rojo
                default:
                    return "default";       // Gris
            }
        }

        public static string ObtenerIcono(string nivel)
        {
            if (string.IsNullOrWhiteSpace(nivel))
                return "fa-bell";

            switch (nivel.ToUpperInvariant())
            {
                case INFO:
                    return "fa-info-circle";
                case SUCCESS:
                    return "fa-check-circle";
                case WARNING:
                    return "fa-exclamation-triangle";
                case ERROR:
                    return "fa-exclamation-circle";
                default:
                    return "fa-bell";
            }
        }


        // ============================================
        // PLANTILLAS DE MENSAJES
        // ============================================
        
        public static class Plantillas
        {
            // Solicitudes
            public static string SolicitudNueva(int codigoSolicitud) =>
                $"Se ha registrado una nueva solicitud AOCR #{codigoSolicitud}";

            public static string SolicitudCambioEstado(int codigoSolicitud, string nuevoEstado) =>
                $"La solicitud #{codigoSolicitud} cambió a estado: {nuevoEstado}";

            public static string SolicitudAprobada(int codigoSolicitud) =>
                $"¡Felicitaciones! La solicitud #{codigoSolicitud} ha sido aprobada";

            public static string SolicitudRechazada(int codigoSolicitud, string motivo) =>
                $"La solicitud #{codigoSolicitud} fue rechazada. Motivo: {motivo}";

            // Inspecciones
            public static string InspeccionProgramada(int codigoInspeccion, DateTime fecha) =>
                $"Inspección #{codigoInspeccion} programada para {fecha:dd/MM/yyyy HH:mm}";

            public static string InspeccionCompletada(int codigoInspeccion) =>
                $"La inspección #{codigoInspeccion} ha sido completada";

            public static string InspeccionAplazada(int codigoInspeccion, string motivo) =>
                $"Inspección #{codigoInspeccion} aplazada. Motivo: {motivo}";

            // Pagos
            public static string PagoRecibido(int codigoPago, decimal monto) =>
                $"Se ha registrado el pago #{codigoPago} por ${monto:N2}";

            public static string OrdenGenerada(string numeroOrden, decimal monto) =>
                $"Orden de recaudación {numeroOrden} generada por ${monto:N2}";

            public static string PagoVencido(string numeroOrden, DateTime fechaVencimiento) =>
                $"La orden {numeroOrden} venció el {fechaVencimiento:dd/MM/yyyy}. Por favor regularice su pago.";

            // Documentos
            public static string SubsanacionSolicitada(int codigoSolicitud, string documentos) =>
                $"Se requiere subsanar documentos para solicitud #{codigoSolicitud}: {documentos}";

            public static string SubsanacionCompletada(int codigoSolicitud) =>
                $"Documentos de solicitud #{codigoSolicitud} subsanados correctamente";

            public static string DocumentoFaltante(int codigoSolicitud, string documento) =>
                $"Documento faltante en solicitud #{codigoSolicitud}: {documento}";

            // Certificados
            public static string CertificadoEmitido(string numeroCertificado) =>
                $"Certificado AOCR {numeroCertificado} emitido exitosamente";

            public static string CertificadoProximoVencer(string numeroCertificado, int diasRestantes) =>
                $"El certificado {numeroCertificado} vence en {diasRestantes} días. Por favor inicie proceso de renovación.";

            public static string CertificadoVencido(string numeroCertificado, DateTime fechaVencimiento) =>
                $"¡ATENCIÓN! El certificado {numeroCertificado} venció el {fechaVencimiento:dd/MM/yyyy}";

            // Sistema
            public static string SistemaMantenimiento(DateTime inicio, DateTime fin) =>
                $"Mantenimiento programado desde {inicio:dd/MM HH:mm} hasta {fin:dd/MM HH:mm}";

            public static string SistemaActualizado(string version) =>
                $"Sistema actualizado a versión {version}";
        }


        // ============================================
        // URLS DE REDIRECCIÓN
        // ============================================
        
        public static class Urls
        {
            public static string Solicitud(int codigoSolicitud) =>
                $"/SolicitudAOCR/Detalle/{codigoSolicitud}";

            public static string Inspeccion(int codigoInspeccion) =>
                $"/Inspeccion/Detalle/{codigoInspeccion}";

            public static string Pago(int codigoPago) =>
                $"/Pago/Ver/{codigoPago}";

            public static string Hallazgo(int codigoHallazgo) =>
                $"/Inspeccion/VerHallazgo/{codigoHallazgo}";

            public static string OrdenRecaudacion(int codigoOrden) =>
                $"/OrdenRecaudacion/Detalle/{codigoOrden}";

            public static string Subsanacion(int codigoSubsanacion) =>
                $"/Subsanacion/Detalle/{codigoSubsanacion}";

            public static string Certificado(string numeroCertificado) =>
                $"/Certificado/Ver/{numeroCertificado}";
        }


        // ============================================
        // CONFIGURACIÓN DE ENVÍO
        // ============================================
        
        /// <summary>
        /// Define si una notificación debe enviarse también por email
        /// </summary>
        public static bool RequiereEmail(string evento)
        {
            if (string.IsNullOrWhiteSpace(evento))
                return false;

            var eventosConEmail = new[]
            {
                SOLICITUD_APROBADA,
                SOLICITUD_RECHAZADA,
                INSPECCION_PROGRAMADA,
                PAGO_RECIBIDO,
                PAGO_VENCIDO,
                SUBSANACION_SOLICITADA,
                CERTIFICADO_EMITIDO,
                CERTIFICADO_PROXIMO_VENCER,
                CERTIFICADO_VENCIDO
            };

            return eventosConEmail.Contains(evento.ToUpperInvariant());
        }

        /// <summary>
        /// Define si una notificación es crítica y debe mostrarse como popup
        /// </summary>
        public static bool EsCritica(string evento)
        {
            if (string.IsNullOrWhiteSpace(evento))
                return false;

            var eventosCriticos = new[]
            {
                SOLICITUD_RECHAZADA,
                PAGO_VENCIDO,
                CERTIFICADO_VENCIDO,
                INSPECCION_APLAZADA
            };

            return eventosCriticos.Contains(evento.ToUpperInvariant());
        }

        /// <summary>
        /// Obtiene el tiempo de vida de una notificación en días
        /// </summary>
        public static int ObtenerTiempoVida(string nivel)
        {
            if (string.IsNullOrWhiteSpace(nivel))
                return 30; // default

            switch (nivel.ToUpperInvariant())
            {
                case INFO:
                    return 7;       // 1 semana
                case SUCCESS:
                    return 15;      // 2 semanas
                case WARNING:
                    return 30;      // 1 mes
                case ERROR:
                    return 90;      // 3 meses (críticas se conservan más tiempo)
                default:
                    return 30;
            }
        }
    }
}
