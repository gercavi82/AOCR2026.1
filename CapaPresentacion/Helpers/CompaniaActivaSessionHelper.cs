using System;
using System.Web;

namespace CapaPresentacion.Helpers
{
    public static class CompaniaActivaSessionHelper
    {
        public const string SessionCompaniaActivaCodigo = "CompaniaActivaCodigo";
        public const string SessionCompaniaActivaNombre = "CompaniaActivaNombre";
        public const string SessionCompaniaPendienteReturnUrl = "PostLoginReturnUrl";

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
            session.Remove("EmpresaCodigo");
            session.Remove("EmpresaNombre");
        }
    }
}
