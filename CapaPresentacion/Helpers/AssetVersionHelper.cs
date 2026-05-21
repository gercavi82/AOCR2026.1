using System;
using System.Configuration;
using System.Web.Mvc;

namespace CapaPresentacion.Helpers
{
    public static class AssetVersionHelper
    {
        private const string FrontVersionKey = "VersionFront";
        private const string FallbackVersionKey = "AOCR:UI:Version";
        private static readonly object SyncRoot = new object();
        private static string _cachedVersion;

        public static string GetFrontVersion()
        {
            if (!string.IsNullOrWhiteSpace(_cachedVersion))
            {
                return _cachedVersion;
            }

            lock (SyncRoot)
            {
                if (!string.IsNullOrWhiteSpace(_cachedVersion))
                {
                    return _cachedVersion;
                }

                var version = ConfigurationManager.AppSettings[FrontVersionKey];
                if (string.IsNullOrWhiteSpace(version))
                {
                    version = ConfigurationManager.AppSettings[FallbackVersionKey];
                }

                if (string.IsNullOrWhiteSpace(version))
                {
                    version = typeof(MvcApplication).Assembly.GetName().Version.ToString();
                }

                _cachedVersion = version.Trim();
                return _cachedVersion;
            }
        }

        public static string VersionedContent(UrlHelper url, string virtualPath)
        {
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            if (string.IsNullOrWhiteSpace(virtualPath))
            {
                return string.Empty;
            }

            return AppendVersion(url.Content(virtualPath));
        }

        public static string AppendVersion(string resolvedUrl)
        {
            if (string.IsNullOrWhiteSpace(resolvedUrl))
            {
                return string.Empty;
            }

            var separator = resolvedUrl.IndexOf('?') >= 0 ? "&" : "?";
            return resolvedUrl + separator + "v=" + Uri.EscapeDataString(GetFrontVersion());
        }
    }
}