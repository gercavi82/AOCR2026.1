using System;

namespace CapaDatos.Constants
{
    /// <summary>
    /// Constantes para estados de Órdenes de Recaudación
    /// Elimina magic strings y centraliza valores
    /// </summary>
    public static class EstadoOrden
    {
        /// <summary>
        /// Orden creada pero no finalizada (puede editarse)
        /// </summary>
        public const string Borrador = "BORRADOR";

        /// <summary>
        /// Orden generada y lista para envío (no editable)
        /// </summary>
        public const string Generada = "GENERADA";

        /// <summary>
        /// Orden pendiente de pago
        /// </summary>
        public const string Pendiente = "PENDIENTE";

        /// <summary>
        /// Orden con pago completado
        /// </summary>
        public const string Completada = "COMPLETADA";

        /// <summary>
        /// Orden facturada (sinónimo de pagada)
        /// </summary>
        public const string Facturada = "FACTURADA";

        /// <summary>
        /// Orden pagada (sinónimo de completada)
        /// </summary>
        public const string Pagada = "PAGADA";

        /// <summary>
        /// Orden anulada (no puede modificarse ni pagarse)
        /// </summary>
        public const string Anulada = "ANULADA";

        /// <summary>
        /// Verifica si un estado permite edición
        /// </summary>
        public static bool PermiteEditar(string estado)
        {
            return string.Equals(estado, Borrador, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifica si un estado permite cambio de estado
        /// </summary>
        public static bool PermiteCambiarEstado(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;

            return string.Equals(estado, Borrador, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Generada, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Pendiente, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifica si un estado es final (no puede cambiar)
        /// </summary>
        public static bool EsEstadoFinal(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;

            return string.Equals(estado, Pagada, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Anulada, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Completada, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Facturada, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifica si un estado representa pago completado
        /// </summary>
        public static bool EsPagado(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;

            return string.Equals(estado, Pagada, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Completada, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Facturada, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Obtiene el color CSS para el badge según el estado
        /// </summary>
        public static string ObtenerColorBadge(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return "secondary";

            switch (estado.ToUpperInvariant())
            {
                case Borrador:
                    return "secondary";
                case Pendiente:
                case Generada:
                    return "warning";
                case Facturada:
                case Completada:
                case Pagada:
                    return "success";
                case Anulada:
                    return "danger";
                default:
                    return "info";
            }
        }

        /// <summary>
        /// Obtiene todos los estados válidos
        /// </summary>
        public static string[] ObtenerTodos()
        {
            return new[]
            {
                Borrador,
                Generada,
                Pendiente,
                Completada,
                Facturada,
                Pagada,
                Anulada
            };
        }
    }

    /// <summary>
    /// Constantes para estados de Pagos
    /// </summary>
    public static class EstadoPago
    {
        /// <summary>
        /// Pago registrado pero no validado
        /// </summary>
        public const string Pendiente = "PENDIENTE";

        /// <summary>
        /// Pago validado por administrador
        /// </summary>
        public const string Validado = "VALIDADO";

        /// <summary>
        /// Pago aprobado (sinónimo de validado)
        /// </summary>
        public const string Aprobado = "APROBADO";

        /// <summary>
        /// Pago rechazado
        /// </summary>
        public const string Rechazado = "RECHAZADO";

        /// <summary>
        /// Pago anulado
        /// </summary>
        public const string Anulado = "ANULADO";

        /// <summary>
        /// Verifica si un estado de pago es final
        /// </summary>
        public static bool EsEstadoFinal(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;

            return string.Equals(estado, Validado, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Aprobado, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Rechazado, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Anulado, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Constantes para estados de Email Queue
    /// </summary>
    public static class EstadoEmail
    {
        /// <summary>
        /// Email pendiente de envío
        /// </summary>
        public const string Pendiente = "Pendiente";

        /// <summary>
        /// Email enviado exitosamente
        /// </summary>
        public const string Enviado = "Enviado";

        /// <summary>
        /// Email falló después de reintentos
        /// </summary>
        public const string Fallido = "Fallido";

        /// <summary>
        /// Email en proceso de envío
        /// </summary>
        public const string Procesando = "Procesando";

        /// <summary>
        /// Email cancelado/descartado
        /// </summary>
        public const string Cancelado = "Cancelado";
    }

    /// <summary>
    /// Constantes para estados de Documentos
    /// </summary>
    public static class EstadoDocumento
    {
        public const string Pendiente = "PENDIENTE";
        public const string Aprobado = "APROBADO";
        public const string Rechazado = "RECHAZADO";
        public const string Revision = "REVISION";
    }

    /// <summary>
    /// Constantes para estados de Solicitud
    /// </summary>
    public static class EstadoSolicitud
    {
        public const string Pendiente = "Pendiente";
        public const string EnRevision = "En Revisión";
        public const string DocumentacionCompleta = "Documentación Completa";
        public const string PagoPendiente = "Pago Pendiente";
        public const string PagoValidado = "Pago Validado";
        public const string InspeccionProgramada = "Inspección Programada";
        public const string InspeccionRealizada = "Inspección Realizada";
        public const string Aprobada = "Aprobada";
        public const string Rechazada = "Rechazada";
        public const string CertificadoEmitido = "Certificado Emitido";
        public const string Anulada = "Anulada";

        private static readonly string[] EstadosValidos =
        {
            Pendiente,
            EnRevision,
            DocumentacionCompleta,
            PagoPendiente,
            PagoValidado,
            InspeccionProgramada,
            InspeccionRealizada,
            Aprobada,
            Rechazada,
            CertificadoEmitido,
            Anulada
        };

        public static string Normalizar(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
                return Pendiente;

            var trimmed = estado.Trim();
            switch (trimmed.ToUpperInvariant())
            {
                case "BORRADOR":
                case "PENDIENTE":
                    return Pendiente;
                case "ENVIADO":
                case "ENVIADO_A_INSPECTOR":
                case "INSPECCION_ASIGNADA":
                case "PREPARANDO":
                    return EnRevision;
                case "DOCUMENTOS_COMPLETOS":
                case "DOCUMENTACION_COMPLETA":
                    return DocumentacionCompleta;
                case "PAGO_PENDIENTE":
                    return PagoPendiente;
                case "PAGO_VALIDADO":
                case "PAGO_VALIDADO_ADMIN":
                    return PagoValidado;
                case "INSPECCION_PROGRAMADA":
                case "INSPECCION_A_PROGRAMAR":
                    return InspeccionProgramada;
                case "INSPECCION_REALIZADA":
                case "INSPECCION_COMPLETADA":
                    return InspeccionRealizada;
                case "APROBADO":
                case "APROBADO_POR_INSPECTOR":
                case "APROBADO_POR_DIRECCION":
                    return Aprobada;
                case "RECHAZADO":
                case "OBSERVADO":
                case "OBSERVADO_JEFATURA":
                case "RECHAZADO_POR_DIRECCION":
                    return Rechazada;
                case "LEGALIZADO":
                case "VALIDADO_TECNICAMENTE":
                case "CERTIFICADO_LEGALIZADO":
                case "CERTIFICADO_EMITIDO":
                    return CertificadoEmitido;
                default:
                    // Si ya es un estado válido, regresar su versión corregida
                    foreach (var valido in EstadosValidos)
                    {
                        if (string.Equals(valido, trimmed, StringComparison.OrdinalIgnoreCase))
                            return valido;
                    }
                    return Pendiente;
            }
        }

        public static bool PermiteEdicion(string estado)
        {
            return string.Equals(Normalizar(estado), Pendiente, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EsEstadoValido(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;
            var normalized = Normalizar(estado);
            foreach (var valido in EstadosValidos)
            {
                if (string.Equals(normalized, valido, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

    }
}
