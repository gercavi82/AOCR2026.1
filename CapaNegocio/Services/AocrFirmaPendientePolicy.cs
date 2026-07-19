using System;
using CapaDatos.Constants;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Regla compartida por bandejas y contadores de firma institucional.
    /// Evita que el badge cuente una colección distinta de la pantalla destino.
    /// </summary>
    public static class AocrFirmaPendientePolicy
    {
        public const string DocumentoAocr = "AOCR";
        public const string DocumentoCondiciones = "CONDICIONES";

        public static bool Coincide(AocrBandejaDocumentoRow fila, string documentoPendiente)
        {
            if (string.IsNullOrWhiteSpace(documentoPendiente))
            {
                return true;
            }

            if (string.Equals(documentoPendiente.Trim(), DocumentoAocr, StringComparison.OrdinalIgnoreCase))
            {
                return EsAocrPendienteFirma(fila);
            }

            if (string.Equals(documentoPendiente.Trim(), DocumentoCondiciones, StringComparison.OrdinalIgnoreCase))
            {
                return EsCondicionesPendienteFirma(fila);
            }

            return false;
        }

        public static bool EsAocrPendienteFirma(AocrBandejaDocumentoRow fila)
        {
            return fila != null
                && string.Equals(fila.EstadoDocumentoAocr, AocrEstadosProceso.PendienteFirmaAocrDirdac, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EsCondicionesPendienteFirma(AocrBandejaDocumentoRow fila)
        {
            return fila != null
                && string.Equals(fila.EstadoDocumentoCondiciones, AocrEstadosProceso.PendienteFirmaCondicionesDcav, StringComparison.OrdinalIgnoreCase);
        }
    }
}
