using System;
using System.Linq;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaPresentacion.Helpers
{
    /// <summary>
    /// Restaura la compañía activa en sesión cuando se perdió tras reciclaje de IIS
    /// pero el usuario sigue editando una solicitud válida.
    /// </summary>
    public static class CompaniaActivaRecoveryHelper
    {
        public static int ResolveUsuarioIdSolicitud(System.Web.HttpSessionStateBase session, int userIdHint)
        {
            if (userIdHint > 0)
            {
                return userIdHint;
            }

            if (session == null)
            {
                return 0;
            }

            foreach (var key in new[] { "IdUsuario", "UserId", "CodigoUsuario" })
            {
                var raw = session[key];
                int parsed;
                if (raw != null && int.TryParse(raw.ToString(), out parsed) && parsed > 0)
                {
                    return parsed;
                }
            }

            return 0;
        }

        public static bool TryRestoreFromSolicitud(System.Web.HttpSessionStateBase session, int solicitudId, int userId, bool esAdmin)
        {
            if (session == null || solicitudId <= 0)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(CompaniaActivaSessionHelper.ObtenerCodigo(session)))
            {
                return true;
            }

            userId = ResolveUsuarioIdSolicitud(session, userId);

            SolicitudAOCR solicitud;
            try
            {
                solicitud = new SolicitudAOCRDAO().ObtenerPorId(solicitudId);
            }
            catch
            {
                return false;
            }

            if (solicitud == null)
            {
                return false;
            }

            if (!esAdmin && solicitud.CodigoUsuario != userId)
            {
                return false;
            }

            var codigo = ObtenerPrimerToken(solicitud.CompaniasSeleccionadas);
            if (string.IsNullOrWhiteSpace(codigo))
            {
                codigo = (solicitud.CodigoOaci ?? string.Empty).Trim();
            }

            if (string.IsNullOrWhiteSpace(codigo))
            {
                return false;
            }

            var nombre = !string.IsNullOrWhiteSpace(solicitud.NombreOperador)
                ? solicitud.NombreOperador.Trim()
                : codigo;

            CompaniaActivaSessionHelper.Establecer(session, codigo, nombre);
            System.Diagnostics.Trace.TraceInformation(
                "[AOCR][COMPANIA] Compañía activa restaurada desde solicitud=" + solicitudId +
                "; Codigo=" + codigo +
                "; UsuarioId=" + userId);

            return true;
        }

        private static string ObtenerPrimerToken(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return string.Empty;
            }

            return valor
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => (v ?? string.Empty).Trim())
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                ?? string.Empty;
        }
    }
}
