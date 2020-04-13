﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ComplaintTracking.ExtensionMethods
{
    public static class HttpRequestExtensions
    {
        public static bool IsLocal(this HttpRequest req)
        {
            if (req.Host.HasValue)
            {
                return req.Host.Value.StartsWith("localhost:");
            }
            return false;
        }

        public static string AbsoluteAction(
            this IUrlHelper url,
            string action,
            string controller,
            object routeValues = null
        )
        {
            string scheme = url.ActionContext.HttpContext.Request.Scheme;
            return url.Action(action, controller, routeValues, scheme);
        }
    }
}
