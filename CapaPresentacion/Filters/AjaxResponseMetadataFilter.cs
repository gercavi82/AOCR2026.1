using System;
using System.Collections;
using System.Reflection;
using System.Web.Mvc;

namespace CapaPresentacion.Filters
{
    public sealed class AjaxResponseMetadataFilter : ActionFilterAttribute
    {
        public const string InternalCodeKey = "__AocrAjaxInternalCode";
        public const string ExceptionKey = "__AocrAjaxException";

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            if (filterContext == null || filterContext.HttpContext == null ||
                !IsAjaxLikeRequest(filterContext.HttpContext.Request))
            {
                return;
            }

            if (filterContext.Exception != null)
            {
                filterContext.HttpContext.Items[ExceptionKey] = filterContext.Exception;
            }

            var json = filterContext.Result as JsonResult;
            var code = json != null ? ReadCode(json.Data) : null;
            if (code != null)
            {
                filterContext.HttpContext.Items[InternalCodeKey] = code;
            }
        }

        private static bool IsAjaxLikeRequest(System.Web.HttpRequestBase request)
        {
            if (request == null) return false;
            return request.IsAjaxRequest()
                || string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                || (request.AcceptTypes != null && Array.Exists(request.AcceptTypes,
                    value => !string.IsNullOrWhiteSpace(value) && value.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static object ReadCode(object data)
        {
            if (data == null) return null;

            var dictionary = data as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = Convert.ToString(entry.Key);
                    if (string.Equals(key, "code", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(key, "codigo", StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }
            }

            var type = data.GetType();
            var property = type.GetProperty("code", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                ?? type.GetProperty("codigo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            return property != null ? property.GetValue(data, null) : null;
        }
    }
}
