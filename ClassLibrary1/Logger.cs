using System;
using System.Diagnostics;

namespace CapaUtilidades
{
    public static class Logger
    {
        public static void Info(string message)
        {
            Trace.TraceInformation("[INFO] " + message);
        }

        public static void Warn(string message)
        {
            Trace.TraceWarning("[WARN] " + message);
        }

        public static void Error(string message, Exception ex)
        {
            Trace.TraceError("[ERROR] " + message + " :: " + ex);
        }
    }
}
