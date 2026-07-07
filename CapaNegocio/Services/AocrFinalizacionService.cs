using System;
using System.Diagnostics;
using CapaDatos.Constants;
using CapaDatos.DAOs;

namespace CapaNegocio.Services
{
    public sealed class AocrFinalizacionResultado
    {
        public bool Finalizado { get; set; }
        public string EstadoNuevo { get; set; }
        public string Motivo { get; set; }
    }

    public sealed class AocrFinalizacionService
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly AocrFirmaDocumentoDAO _firmaDocumentoDao = new AocrFirmaDocumentoDAO();
        private readonly HistorialEstadoDAO _historialEstadoDao = new HistorialEstadoDAO();

        public AocrFinalizacionResultado IntentarFinalizarEmision(int solicitudId, int usuarioId, Func<string, bool> rutaExiste)
        {
            var resultado = new AocrFinalizacionResultado { Finalizado = false };
            try
            {
                var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
                if (solicitud == null)
                {
                    resultado.Motivo = "Solicitud no encontrada.";
                    LogPendiente(solicitudId, resultado.Motivo);
                    return resultado;
                }

                var estadoAnterior = EstadoSolicitud.Normalizar(solicitud.Estado);
                if (string.Equals(estadoAnterior, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoAnterior, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoAnterior, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase))
                {
                    resultado.Finalizado = true;
                    resultado.EstadoNuevo = estadoAnterior;
                    resultado.Motivo = "La solicitud ya se encuentra liberada.";
                    LogOk(solicitudId, estadoAnterior, true, true);
                    return resultado;
                }

                var firmaAocr = _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
                var firmaCondiciones = _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES_LIMITACIONES");
                if (firmaCondiciones == null)
                {
                    firmaCondiciones = _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES");
                }
                var aocrFirmada = DocumentoFirmadoExiste(firmaAocr != null ? firmaAocr.RutaDocumento : null, rutaExiste);
                var condicionesFirmadas = DocumentoFirmadoExiste(firmaCondiciones != null ? firmaCondiciones.RutaDocumento : null, rutaExiste);

                if (!aocrFirmada || !condicionesFirmadas)
                {
                    resultado.Motivo = "Documentos firmados incompletos. AocrFirmada=" + aocrFirmada + "; CondicionesFirmadas=" + condicionesFirmadas;
                    LogPendiente(solicitudId, resultado.Motivo);
                    return resultado;
                }

                var estadoNuevo = EstadoSolicitud.AOCR_Legalizado;
                var actualizado = _solicitudDao.CambiarEstado(
                    solicitudId,
                    estadoNuevo,
                    usuarioId,
                    "AOCR y Condiciones firmadas. Documentos finales disponibles.");

                if (!actualizado)
                {
                    resultado.Motivo = "No se pudo actualizar el estado final.";
                    LogPendiente(solicitudId, resultado.Motivo);
                    return resultado;
                }

                try
                {
                    _historialEstadoDao.RegistrarCambio(
                        solicitudId,
                        estadoAnterior,
                        estadoNuevo,
                        usuarioId,
                        "Liberacion final AOCR por firma institucional completa.");
                }
                catch
                {
                }

                resultado.Finalizado = true;
                resultado.EstadoNuevo = estadoNuevo;
                resultado.Motivo = "Documentos finales liberados.";
                try
                {
                    new AocrEstadoProcesoService().SincronizarDesdeFuentesActuales(
                        solicitudId,
                        "FINALIZAR_AOCR",
                        usuarioId,
                        "DireccionJefaturaTecnica",
                        "Cierre de emision AOCR.");
                }
                catch
                {
                }
                LogOk(solicitudId, estadoNuevo, aocrFirmada, condicionesFirmadas);
                return resultado;
            }
            catch (Exception ex)
            {
                resultado.Motivo = ex.Message;
                Trace.TraceError("[AOCR_FINAL][ERROR] SolicitudId=" + solicitudId + "; Motivo=" + ex.Message + "; Exception=" + ex);
                return resultado;
            }
        }

        private static bool DocumentoFirmadoExiste(string ruta, Func<string, bool> rutaExiste)
        {
            return !string.IsNullOrWhiteSpace(ruta)
                && rutaExiste != null
                && rutaExiste(ruta);
        }

        private static void LogOk(int solicitudId, string estadoNuevo, bool aocrFirmada, bool condicionesFirmadas)
        {
            Trace.TraceInformation(
                "[AOCR_FINAL][OK] SolicitudId=" + solicitudId +
                "; EstadoNuevo=" + (estadoNuevo ?? string.Empty) +
                "; AocrFirmada=" + aocrFirmada +
                "; CondicionesFirmadas=" + condicionesFirmadas);
        }

        private static void LogPendiente(int solicitudId, string motivo)
        {
            Trace.TraceInformation("[AOCR_FINAL][PENDIENTE] SolicitudId=" + solicitudId + "; Motivo=" + (motivo ?? string.Empty));
        }
    }
}
