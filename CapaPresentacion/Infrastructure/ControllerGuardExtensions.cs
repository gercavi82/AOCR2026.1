using System.Web.Mvc;

namespace CapaPresentacion.Infrastructure
{
    public static class ControllerGuardExtensions
    {
        private static readonly IUserContextAccessor _userContext = new UserContextAccessor();

        public static bool TryGetSessionUserId(this Controller controller, out int userId)
        {
            userId = 0;
            if (controller == null)
            {
                return false;
            }

            return _userContext.TryGetUserId(controller.Session, out userId);
        }

        public static bool TryGetSessionCodigoUsuario(this Controller controller, out int codigoUsuario)
        {
            codigoUsuario = 0;
            if (controller == null)
            {
                return false;
            }

            return _userContext.TryGetCodigoUsuario(controller.Session, out codigoUsuario);
        }

        public static JsonResult JsonContextMissing(this Controller controller, string message)
        {
            var safeMessage = string.IsNullOrWhiteSpace(message) ? "SesiÃ³n expirada." : message.Trim();
            return new JsonResult
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    ok = false,
                    success = false,
                    code = "CONTEXT_MISSING",
                    message = safeMessage,
                    mensaje = safeMessage,
                    data = (object)null
                }
            };
        }
    }
}
