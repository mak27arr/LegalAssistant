using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using LegalAssistant.Core.Correlation;

namespace LegalAssistant.Logging.Middleware;

public sealed class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        // Register header write before response is sent to avoid "Headers are read-only" errors
        context.Response.OnStarting(state =>
        {
            var httpContext = (HttpContext)state!;
            try
            {
                sw.Stop();
                var elapsedMs = sw.Elapsed.TotalMilliseconds;
                // only set header if response has not started yet
                if (!httpContext.Response.HasStarted)
                {
                    httpContext.Response.Headers["X-Processing-Time-ms"] = elapsedMs.ToString("F0");
                }
            }
            catch
            {
                // ignore
            }
            return Task.CompletedTask;
        }, context);

        try
        {
            await _next(context);
        }
        finally
        {
            if (sw.IsRunning)
                sw.Stop();
            var elapsedMs = sw.Elapsed.TotalMilliseconds;

            var corr = context.RequestServices.GetService(typeof(ICorrelationContext)) as ICorrelationContext;
            var cid = corr?.CorrelationId ?? "-";

            _logger.LogInformation("Request {Method} {Path} completed in {Elapsed}ms. CorrelationId={CorrelationId}",
                context.Request?.Method, context.Request?.Path, elapsedMs, cid);
        }
    }
}
