using System;

namespace CapaNegocio.Helpers
{
    public static class ValidacionEstadosOR
    {
        public const string BORRADOR = "BORRADOR";
        public const string GENERADA = "GENERADA";
        public const string ENVIADA = "ENVIADA";
        public const string PAGADA = "PAGADA";
        public const string ANULADA = "ANULADA";

        public static void ValidarTransicion(string estadoActual, string estadoNuevo)
        {
            var a = (estadoActual ?? "").Trim().ToUpperInvariant();
            var n = (estadoNuevo ?? "").Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(a)) throw new Exception("Estado actual inválido.");
            if (string.IsNullOrWhiteSpace(n)) throw new Exception("Estado nuevo inválido.");

            // Reglas simples y seguras
            if (a == ANULADA) throw new Exception("Una orden ANULADA no puede cambiar de estado.");
            if (a == PAGADA && n != ANULADA) throw new Exception("Una orden PAGADA solo puede anularse si tu negocio lo permite (ajusta regla).");

            // Flujo típico:
            // BORRADOR -> GENERADA -> ENVIADA -> PAGADA
            if (a == BORRADOR && (n != GENERADA && n != ANULADA))
                throw new Exception("BORRADOR solo puede pasar a GENERADA o ANULADA.");

            if (a == GENERADA && (n != ENVIADA && n != ANULADA))
                throw new Exception("GENERADA solo puede pasar a ENVIADA o ANULADA.");

            if (a == ENVIADA && (n != PAGADA && n != ANULADA))
                throw new Exception("ENVIADA solo puede pasar a PAGADA o ANULADA.");
        }
    }
}
