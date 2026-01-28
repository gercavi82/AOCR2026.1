using System;

namespace CapaNegocio.Helpers
{
    public static class ValidacionEstadosOR
    {
        public const string BORRADOR = "BORRADOR";
        public const string PENDIENTE = "PENDIENTE";
        public const string PROCESADA = "PROCESADA";
        public const string FACTURADA = "FACTURADA";
        public const string COMPLETADA = "COMPLETADA";
        public const string ANULADA = "ANULADA";
        // Legacy/compatibilidad
        public const string GENERADA = "GENERADA";
        public const string ENVIADA = "ENVIADA";
        public const string PAGADA = "PAGADA";
        public const string ORDEN_REQUERIDA = "Orden de Recaudación Requerida";

        public static void ValidarTransicion(string estadoActual, string estadoNuevo)
        {
            var a = (estadoActual ?? "").Trim().ToUpperInvariant();
            var n = (estadoNuevo ?? "").Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(a)) throw new Exception("Estado actual inválido.");
            if (string.IsNullOrWhiteSpace(n)) throw new Exception("Estado nuevo inválido.");

            // Reglas simples y seguras
            if (a == ANULADA) throw new Exception("Una orden ANULADA no puede cambiar de estado.");
            if (a == PAGADA && n != ANULADA) throw new Exception("Una orden PAGADA solo puede anularse si tu negocio lo permite (ajusta regla).");

            // "Orden de Recaudación Requerida" puede ser un estado especial que se asigna externamente
            // Permitir transiciones desde cualquier estado hacia este estado
            if (n == ORDEN_REQUERIDA.ToUpperInvariant()) return;

            // Flujo permitido por DB:
            // BORRADOR -> PENDIENTE
            // PENDIENTE -> PROCESADA
            // PROCESADA -> FACTURADA (aprobada) | PENDIENTE (rechazada)
            // FACTURADA -> COMPLETADA
            if (a == BORRADOR && (n != PENDIENTE && n != ANULADA))
                throw new Exception("BORRADOR solo puede pasar a PENDIENTE o ANULADA.");

            if (a == PENDIENTE && (n != PROCESADA && n != ANULADA))
                throw new Exception("PENDIENTE solo puede pasar a PROCESADA o ANULADA.");

            if (a == PROCESADA && (n != FACTURADA && n != PENDIENTE && n != ANULADA))
                throw new Exception("PROCESADA solo puede pasar a FACTURADA, PENDIENTE o ANULADA.");

            if (a == FACTURADA && (n != COMPLETADA))
                throw new Exception("FACTURADA solo puede pasar a COMPLETADA.");

            // Compatibilidad legacy:
            if (a == GENERADA && (n != PENDIENTE && n != ANULADA))
                throw new Exception("GENERADA solo puede pasar a PENDIENTE o ANULADA.");

            if (a == ENVIADA && (n != PROCESADA && n != ANULADA))
                throw new Exception("ENVIADA solo puede pasar a PROCESADA o ANULADA.");
        }
    }
}
