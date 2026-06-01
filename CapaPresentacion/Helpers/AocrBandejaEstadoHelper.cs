using System;
using System.Collections.Generic;
using CapaDatos.Constants;
using CapaModelo.Common;

namespace CapaPresentacion.Helpers
{
    public static class AocrBandejaEstadoHelper
    {
        private static readonly HashSet<string> EstadosCondiciones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            EstadoSolicitud.GeneradoCondicionesLimitaciones,
            EstadoSolicitud.EnRevisionCoordinadorFinal,
            EstadoSolicitud.EnviadoDcav,
            EstadoSolicitud.FirmadoDcav,
            EstadoSolicitud.Finalizado
        };

        public static string NormalizarEstadoSolicitud(string estado)
        {
            return EstadoSolicitud.Normalizar(estado ?? string.Empty);
        }

        public static bool UsaFlujoCondiciones(AocrBandejaDocumentoRow row)
        {
            if (row == null)
            {
                return false;
            }

            var estadoSolicitud = NormalizarEstadoSolicitud(row.EstadoSolicitudRaw);
            if (EstadosCondiciones.Contains(estadoSolicitud))
            {
                return true;
            }

            return row.TipoSolicitud == 3
                && string.IsNullOrWhiteSpace(row.RutaReconocimientoFirmado)
                && (row.FirmaCondicionesId.HasValue || !string.IsNullOrWhiteSpace(row.RutaCondicionesFirmado));
        }

        public static bool TieneDocumentoFinalFirmado(AocrBandejaDocumentoRow row)
        {
            if (row == null)
            {
                return false;
            }

            if (UsaFlujoCondiciones(row))
            {
                return row.FirmaCondicionesId.HasValue || !string.IsNullOrWhiteSpace(row.RutaCondicionesFirmado);
            }

            return row.FirmaReconocimientoId.HasValue
                || !string.IsNullOrWhiteSpace(row.RutaReconocimientoFirmado)
                || !string.IsNullOrWhiteSpace(row.RutaCertificadoPdf);
        }

        public static bool TieneDocumentoPreliminar(AocrBandejaDocumentoRow row)
        {
            if (row == null)
            {
                return false;
            }

            var estadoSolicitud = NormalizarEstadoSolicitud(row.EstadoSolicitudRaw);
            if (UsaFlujoCondiciones(row))
            {
                return EstadosCondiciones.Contains(estadoSolicitud);
            }

            return !string.IsNullOrWhiteSpace(row.RutaAocrGenerada)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase);
        }

        public static string ObtenerEstadoAocr(AocrBandejaDocumentoRow row)
        {
            if (UsaFlujoCondiciones(row))
            {
                return "No aplica";
            }

            var estadoSolicitud = NormalizarEstadoSolicitud(row != null ? row.EstadoSolicitudRaw : null);
            if (string.IsNullOrWhiteSpace(estadoSolicitud))
            {
                return TieneDocumentoFinalFirmado(row) ? EstadoSolicitud.AOCR_EmitidoRecibido : EstadoSolicitud.AOCR_EnElaboracion;
            }

            return estadoSolicitud;
        }

        public static string ObtenerEstadoCondiciones(AocrBandejaDocumentoRow row)
        {
            if (!UsaFlujoCondiciones(row))
            {
                return "No aplica";
            }

            var estadoSolicitud = NormalizarEstadoSolicitud(row != null ? row.EstadoSolicitudRaw : null);
            if (!string.IsNullOrWhiteSpace(estadoSolicitud))
            {
                return estadoSolicitud;
            }

            return TieneDocumentoFinalFirmado(row)
                ? EstadoSolicitud.FirmadoDcav
                : EstadoSolicitud.GeneradoCondicionesLimitaciones;
        }

        public static string ObtenerEstadoFirma(AocrBandejaDocumentoRow row)
        {
            var estadoSolicitud = NormalizarEstadoSolicitud(row != null ? row.EstadoSolicitudRaw : null);
            if (EsEstadoFirmadoOFinalizado(estadoSolicitud))
            {
                return "Firmado";
            }

            if (TieneDocumentoFinalFirmado(row))
            {
                return "Firmado";
            }

            if (EsEstadoObservado(estadoSolicitud))
            {
                return "Observada";
            }

            if (TieneDocumentoPreliminar(row)
                || string.Equals(estadoSolicitud, EstadoSolicitud.EnviadoDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase))
            {
                return "Pendiente firma";
            }

            return "En gestión";
        }

        public static string ObtenerEstadoFinal(AocrBandejaDocumentoRow row)
        {
            var estadoSolicitud = NormalizarEstadoSolicitud(row != null ? row.EstadoSolicitudRaw : null);
            if (string.Equals(estadoSolicitud, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase))
            {
                return "Finalizado";
            }

            if (string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.FirmadoDcav, StringComparison.OrdinalIgnoreCase))
            {
                return "Firmado";
            }

            if (EsEstadoObservado(estadoSolicitud))
            {
                return "Observada";
            }

            if (TieneDocumentoFinalFirmado(row))
            {
                return "Firmado";
            }

            if (string.Equals(ObtenerEstadoFirma(row), "Pendiente firma", StringComparison.OrdinalIgnoreCase))
            {
                return "Pendiente firma";
            }

            return "En gestión";
        }

        public static string ObtenerBadgeCss(string estado)
        {
            switch ((estado ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "FINALIZADO":
                case "AOCR_EMITIDO_RECIBIDO":
                case "FIRMADO":
                case "FIRMADO_DCAV":
                    return "badge badge-success";
                case "PENDIENTE FIRMA":
                case "AOCR_VALIDADO":
                case "AOCR_LEGALIZADO":
                case "AOCR_EN_REVISION":
                case "ENVIADO_DCAV":
                case "EN_REVISION_COORDINADOR_FINAL":
                    return "badge badge-warning";
                case "OBSERVADA":
                case "OBSERVADO":
                case "RECHAZADA":
                    return "badge badge-danger";
                case "NO APLICA":
                    return "badge badge-secondary";
                default:
                    return "badge badge-info";
            }
        }

        private static bool EsEstadoObservado(string estadoSolicitud)
        {
            return !string.IsNullOrWhiteSpace(estadoSolicitud)
                && (estadoSolicitud.IndexOf("OBSERV", StringComparison.OrdinalIgnoreCase) >= 0
                    || estadoSolicitud.IndexOf("RECHAZ", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool EsEstadoFirmadoOFinalizado(string estadoSolicitud)
        {
            return string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.FirmadoDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase);
        }
    }
}