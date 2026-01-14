using System;
using System.Collections.Generic;
using System.Transactions;
using CapaModelo;
using CapaDatos.DAOs;

namespace CapaNegocio
{
    public static class SolicitudAOCRBL
    {
        private static readonly SolicitudAOCRDAO _dao = new SolicitudAOCRDAO();

        // =====================================================
        // CREACIÓN
        // =====================================================

        public static int CrearSolicitud(SolicitudAOCR solicitud, out string mensaje)
        {
            try
            {
                solicitud.FechaSolicitud = DateTime.Now;
                solicitud.Estado = "PENDIENTE";

                int id = _dao.InsertarConReturn(solicitud);
                mensaje = id > 0 ? "Solicitud creada exitosamente." : "No se pudo generar el ID de solicitud.";
                return id;
            }
            catch (Exception ex)
            {
                mensaje = "Error al crear solicitud: " + ex.Message;
                return 0;
            }
        }

        public static bool GuardarSolicitudIntegral(SolicitudAOCR solicitud, List<ChecklistItem> aeronaves, out string mensaje)
        {
            using (var scope = new TransactionScope())
            {
                try
                {
                    int idSolicitud = CrearSolicitud(solicitud, out mensaje);
                    if (idSolicitud <= 0) return false;

                    // Guardar aeronaves (si aplica)
                    // AeronaveDAO.Insertar(...);

                    scope.Complete();
                    mensaje = $"Solicitud #{idSolicitud} registrada con éxito.";
                    return true;
                }
                catch (Exception ex)
                {
                    mensaje = "Falla técnica en el guardado masivo: " + ex.Message;
                    return false;
                }
            }
        }

        // =====================================================
        // CONSULTAS
        // =====================================================

        public static List<SolicitudAOCR> ListarPorUsuario(int codigoUsuario)
            => _dao.ObtenerPorUsuario(codigoUsuario);

        public static SolicitudAOCR ObtenerPorId(int id)
            => _dao.ObtenerPorId(id);

        public static List<SolicitudAOCR> ObtenerPorEstado(string estado)
            => _dao.ObtenerPorEstado(estado);

        public static List<SolicitudAOCR> ListarPendientesRevision()
            => _dao.ObtenerPendientesRevision();

        // =====================================================
        // ACTUALIZACIÓN GENERAL
        // =====================================================

        public static bool ActualizarEstado(SolicitudAOCR solicitud)
        {
            try
            {
                solicitud.UpdatedAt = DateTime.Now;
                return _dao.ActualizarGeneral(solicitud);
            }
            catch
            {
                return false;
            }
        }

        public static bool CambiarEstado(int idSolicitud, string nuevoEstado, int codigoUsuario, string observaciones, out string mensaje)
        {
            try
            {
                bool ok = _dao.CambiarEstado(idSolicitud, nuevoEstado, codigoUsuario, observaciones);
                mensaje = ok ? "Estado actualizado correctamente." : "No fue posible cambiar el estado.";
                return ok;
            }
            catch (Exception ex)
            {
                mensaje = "Error en el flujo de estados: " + ex.Message;
                return false;
            }
        }

        public static bool MarcarParaInspeccion(int idSolicitud)
        {
            try
            {
                return _dao.CambiarEstado(idSolicitud, "INSPECCION_SOLICITADA", 0, "Cambio automático por sistema");
            }
            catch
            {
                return false;
            }
        }

        // =====================================================
        // INSPECCIONES
        // =====================================================

        public static bool AsignarInspeccion(
            int solicitudId,
            int inspectorId,
            DateTime fecha,
            TimeSpan hora,
            string lugar,
            string comentarios,
            int usuarioId,
            out string mensaje)
        {
            if (fecha.Date < DateTime.Today)
            {
                mensaje = "La fecha programada no puede ser anterior a hoy.";
                return false;
            }

            return _dao.AsignarInspeccion(
                solicitudId,
                inspectorId,
                fecha,
                hora,
                lugar,
                comentarios,
                usuarioId,
                out mensaje);
        }
    }
}
