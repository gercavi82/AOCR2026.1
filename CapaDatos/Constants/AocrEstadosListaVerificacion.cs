using System;

namespace CapaDatos.Constants
{
    /// <summary>
    /// AC-07: Catálogo canónico de estados individuales de la Lista de Verificación (LV).
    /// Se mantienen estrictamente desacoplados del estado global de la solicitud o de la inspección.
    /// </summary>
    public static class AocrEstadosListaVerificacion
    {
        public const string NoCreada = "LV_NO_CREADA";
        public const string Borrador = "LV_BORRADOR";
        public const string EnProceso = "LV_EN_PROCESO";
        public const string Completa = "LV_COMPLETA";
        public const string PendienteFirma = "LV_PENDIENTE_FIRMA";
        public const string Firmada = "LV_FIRMADA";
        public const string Devuelta = "LV_DEVUELTA";
        public const string RequiereCorreccion = "LV_REQUIERE_CORRECCION";
        public const string Anulada = "LV_ANULADA";

        /// <summary>
        /// Normaliza el estado de la LV resolviendo mayúsculas, espacios y variantes históricas.
        /// </summary>
        public static string Normalizar(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return NoCreada;
            }

            var token = estado.Trim().ToUpperInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");

            switch (token)
            {
                case "LV_NO_CREADA":
                case "NO_CREADA":
                case "NOCREADA":
                    return NoCreada;

                case "LV_BORRADOR":
                case "BORRADOR":
                    return Borrador;

                case "LV_EN_PROCESO":
                case "EN_PROCESO":
                case "EN_CURSO":
                case "PROCESO":
                    return EnProceso;

                case "LV_COMPLETA":
                case "COMPLETA":
                case "FINALIZADA":
                case "COMPLETO":
                    return Completa;

                case "LV_PENDIENTE_FIRMA":
                case "PENDIENTE_FIRMA":
                case "POR_FIRMAR":
                    return PendienteFirma;

                case "LV_FIRMADA":
                case "FIRMADA":
                case "FIRMADO":
                    return Firmada;

                case "LV_DEVUELTA":
                case "DEVUELTA":
                    return Devuelta;

                case "LV_REQUIERE_CORRECCION":
                case "REQUIERE_CORRECCION":
                case "OBSERVADA":
                    return RequiereCorreccion;

                case "LV_ANULADA":
                case "ANULADA":
                case "CANCELADA":
                    return Anulada;

                default:
                    return token;
            }
        }

        /// <summary>
        /// Determina si la LV puede ser editada por el Inspector asignado.
        /// Las LV firmadas o anuladas son inmutables.
        /// </summary>
        public static bool EsEditable(string estado)
        {
            var e = Normalizar(estado);
            return e == Borrador || e == EnProceso || e == Completa || e == PendienteFirma || e == Devuelta || e == RequiereCorreccion;
        }

        /// <summary>
        /// Determina si la LV ya ha sido culminada y firmada oficialmente.
        /// </summary>
        public static bool EstaFirmada(string estado)
        {
            return Normalizar(estado) == Firmada;
        }
    }
}
