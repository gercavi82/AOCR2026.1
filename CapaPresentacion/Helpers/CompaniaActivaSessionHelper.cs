using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace CapaPresentacion.Helpers
{
    public static class CompaniaActivaSessionHelper
    {
        public const string SessionCompaniaActivaCodigo = "CompaniaActivaCodigo";
        public const string SessionCompaniaActivaNombre = "CompaniaActivaNombre";
        public const string SessionCompaniaPendienteReturnUrl = "PostLoginReturnUrl";
        public const string SessionCompaniaActivaContextToken = "CompaniaActivaContextToken";

        public static void Establecer(HttpSessionStateBase session, string codigo, string nombre)
        {
            if (session == null)
            {
                return;
            }

            var codigoNormalizado = (codigo ?? string.Empty).Trim();
            var nombreNormalizado = (nombre ?? string.Empty).Trim();

            session[SessionCompaniaActivaCodigo] = codigoNormalizado;
            session[SessionCompaniaActivaNombre] = nombreNormalizado;

            // Compatibilidad con módulos existentes.
            session["EmpresaCodigo"] = codigoNormalizado;
            session["EmpresaNombre"] = nombreNormalizado;
        }

        public static string ObtenerCodigo(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return string.Empty;
            }

            var codigo = (session[SessionCompaniaActivaCodigo] as string ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                return codigo;
            }

            return (session["EmpresaCodigo"] as string ?? string.Empty).Trim();
        }

        public static string ObtenerNombre(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return string.Empty;
            }

            var nombre = (session[SessionCompaniaActivaNombre] as string ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(nombre))
            {
                return nombre;
            }

            return (session["EmpresaNombre"] as string ?? string.Empty).Trim();
        }

        public static void Limpiar(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return;
            }

            session.Remove(SessionCompaniaActivaCodigo);
            session.Remove(SessionCompaniaActivaNombre);
            session.Remove(SessionCompaniaActivaContextToken);
            session.Remove("EmpresaCodigo");
            session.Remove("EmpresaNombre");
        }

        public static string GenerarTokenContexto(HttpSessionStateBase session, int usuarioId)
        {
            if (session == null || usuarioId <= 0)
            {
                return string.Empty;
            }

            var codigo = ObtenerCodigo(session);
            var token = Convert.ToBase64String(
                SHA256.Create().ComputeHash(
                    Encoding.UTF8.GetBytes(usuarioId + "|" + codigo + "|" + DateTime.UtcNow.Ticks)));

            session[SessionCompaniaActivaContextToken] = token;
            return token;
        }

        public static bool ValidarTokenContexto(HttpSessionStateBase session, int usuarioId, string token)
        {
            if (session == null || usuarioId <= 0 || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var esperado = session[SessionCompaniaActivaContextToken] as string;
            return !string.IsNullOrWhiteSpace(esperado)
                && string.Equals(esperado.Trim(), token.Trim(), StringComparison.Ordinal);
        }

        public static void LimpiarDatosTemporalesCambioCompania(HttpSessionStateBase session, int usuarioId)
        {
            if (session == null)
            {
                return;
            }

            session.Remove(SessionCompaniaActivaContextToken);
            session.Remove("TieneOrdenGenerada");
            session.Remove("TieneOrdenBorrador");
            session.Remove("TieneOrdenPendienteProceso");
            session.Remove("TieneOrdenPendienteComprobante");

            if (usuarioId > 0)
            {
                session.Remove("_Sidebar_OrdenStatus_" + usuarioId);
            }
        }
    }
}
