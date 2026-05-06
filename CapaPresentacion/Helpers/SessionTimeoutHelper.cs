using System;
using System.Web.Security;

namespace CapaPresentacion.Helpers
{
    public static class SessionTimeoutHelper
    {
        private const int DefaultWarningMinutes = 5;

        public static int GetTimeoutMinutes()
        {
            return Math.Max(1, (int)Math.Ceiling(FormsAuthentication.Timeout.TotalMinutes));
        }

        public static int GetWarningMinutes()
        {
            var timeoutMinutes = GetTimeoutMinutes();
            if (timeoutMinutes <= 1)
            {
                return 1;
            }

            return Math.Min(DefaultWarningMinutes, timeoutMinutes - 1);
        }
    }
}