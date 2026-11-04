using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MuscleCuties.Server;

/// <summary>
/// Writes one compact log line per request so deployed latency can be attributed
/// to a route template instead of the whole function. Only the method, route
/// template, status code and elapsed milliseconds are recorded: never a query
/// string, body, header, token, user identifier or health value.
/// </summary>
public static class RequestTimingTelemetry
{
    public const string UnmatchedRouteTemplate = "(unmatched)";
    private const int ElapsedMillisecondsPrecision = 1;
    private const string MessageTemplate =
        "Request {Method} {RouteTemplate} {StatusCode} {ElapsedMilliseconds} cold={ColdStart} requestId={RequestId}";
    private static int handledRequestCount;

    public static IApplicationBuilder UseRequestTimingTelemetry(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.ApplicationServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(RequestTimingTelemetry));

        return app.Use(async (context, next) =>
        {
            var isColdStart = Interlocked.Increment(ref handledRequestCount) == 1;
            var startedAt = Stopwatch.GetTimestamp();
            try
            {
                await next(context).ConfigureAwait(false);
            }
            finally
            {
                var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                var routeTemplate = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                    ?? UnmatchedRouteTemplate;

                logger.LogInformation(
                    MessageTemplate,
                    context.Request.Method,
                    routeTemplate,
                    context.Response.StatusCode,
                    Math.Round(elapsedMilliseconds, ElapsedMillisecondsPrecision),
                    isColdStart,
                    context.TraceIdentifier);
            }
        });
    }
}
