using System;
using CapaDatos.Constants;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Regla compartida por bandejas y contadores de firma institucional.
    /// Segrega la firma de Condiciones y Limitaciones (DIRCAV) de la firma de AOCR (DIRDAC).
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
            if (fila == null) return false;
            if (fila.FirmaReconocimientoId.GetValueOrDefault() > 0) return false;
            if (fila.TipoSolicitud == 3) return false;

            if (!string.IsNullOrWhiteSpace(fila.EstadoDocumentoAocr))
            {
                return string.Equals(fila.EstadoDocumentoAocr, AocrEstadosProceso.PendienteFirmaAocrDirdac, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fila.EstadoDocumentoAocr, AocrEstadosProceso.AocrPendienteDirdac, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fila.EstadoDocumentoAocr, AocrEstadosProceso.AocrListoParaFirma, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(fila.EstadoDocumentoCondiciones))
            {
                // Si la fila solo tiene estado de condiciones, no asumir AOCR pendiente
                return false;
            }

            return fila.CertificadoId.GetValueOrDefault() <= 0;
        }

        public static bool EsCondicionesPendienteFirma(AocrBandejaDocumentoRow fila)
        {
            if (fila == null) return false;
            if (fila.FirmaCondicionesId.GetValueOrDefault() > 0) return false;

            if (!string.IsNullOrWhiteSpace(fila.EstadoDocumentoCondiciones))
            {
                return string.Equals(fila.EstadoDocumentoCondiciones, AocrEstadosProceso.PendienteFirmaCondicionesDcav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fila.EstadoDocumentoCondiciones, AocrEstadosProceso.ClPendienteFirmaDircav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fila.EstadoDocumentoCondiciones, AocrEstadosProceso.CondicionesListasParaFirma, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(fila.EstadoDocumentoAocr))
            {
                // Si la fila solo tiene estado de AOCR, no asumir Condiciones pendiente
                return false;
            }

            return fila.CertificadoId.GetValueOrDefault() <= 0;
        }
    }
}
