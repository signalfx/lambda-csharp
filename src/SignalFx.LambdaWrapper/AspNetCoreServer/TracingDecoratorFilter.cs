using Microsoft.AspNetCore.Mvc.Filters;
using OpenTracing;
using OpenTracing.Util;
using SignalFx.Tracing;

namespace SignalFx.LambdaWrapper.AspNetCoreServer
{
    public class TracingDecoratorFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            ISpan span = GlobalTracer.Instance?.ActiveSpan;
            if (span == null)
            {
                return;
            }

            // Build a more meaningful operation name.
            string routeTemplate = context.ActionDescriptor?.AttributeRouteInfo?.Template;
            span.SetOperationName(routeTemplate);

            // Enrich the span with headers or other data that is relevant to the specific context.
            // For example the "User-Agent" header.
            var httpMethod = context.RouteData?.Values["action"]?.ToString().ToUpperInvariant();
            span.SetTag("http.method", httpMethod);
            if (context.HttpContext.Request.Headers.TryGetValue("User-Agent", out var userAgent))
            {
                span.SetTag("User-Agent", userAgent);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            ISpan span = GlobalTracer.Instance?.ActiveSpan;
            if (span == null)
            {
                return;
            }

            if (context.Exception != null)
            {
                // Use SignalFx Span type to leverage the helper method to set error information tags.
                var sfxSpan = Tracer.Instance?.ActiveScope?.Span;
                sfxSpan?.SetException(context.Exception);
            }
        }
    }
}
