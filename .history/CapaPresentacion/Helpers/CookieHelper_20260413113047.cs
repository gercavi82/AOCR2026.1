using System;
using System.Reflection;
using System.Web;

namespace CapaPresentacion.Helpers
{
    /// <summary>
    /// Helper centralizado para compatibilidad de cookies.
    /// Resuelve MissingMethodException en servidores con .NET &lt; 4.7.2
    /// donde HttpCookie.SameSite no existe.
    /// </summary>
    public static class CookieHelper
    {
        private static readonly bool _sameSiteSupported;
        private static readonly PropertyInfo _sameSiteProp;
        private static readonly object _sameSiteLax;

        static CookieHelper()
        {
            try
            {
                _sameSiteProp = typeof(HttpCookie).GetProperty("SameSite",
                    BindingFlags.Public | BindingFlags.Instance);

                if (_sameSiteProp != null && _sameSiteProp.CanWrite)
                {
                    // Resolve SameSiteMode.Lax (value = 1) via enum type
                    var enumType = _sameSiteProp.PropertyType;
                    _sameSiteLax = Enum.ToObject(enumType, 1); // Lax = 1
                    _sameSiteSupported = true;
                }
            }
            catch
            {
                _sameSiteSupported = false;
            }
        }

        /// <summary>
        /// Assigns SameSite=Lax to the cookie if the runtime supports it.
        /// Safe to call on any .NET 4.x version.
        /// </summary>
        public static void SetSameSiteLax(HttpCookie cookie)
        {
            if (cookie == null || !_sameSiteSupported)
                return;

            try
            {
                _sameSiteProp.SetValue(cookie, _sameSiteLax);
            }
            catch
            {
                // Silently ignore - server doesn't support SameSite
            }
        }
    }
}
