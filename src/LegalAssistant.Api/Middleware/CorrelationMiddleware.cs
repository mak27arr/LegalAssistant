using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using LegalAssistant.Core.Correlation;

namespace LegalAssistant.Api.Middleware;

public sealed class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;

    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
            context.Request.Headers["X-Correlation-Id"] = correlationId;
        }

        // Try to resolve correlation context; do not throw when absent.
        var corr = context.RequestServices.GetService<ICorrelationContext>();
        if (corr != null)
        {
            corr.CorrelationId = correlationId;
        }

        var scopeCorrelationId = corr?.CorrelationId ?? correlationId;
        context.Response.Headers["X-Correlation-Id"] = scopeCorrelationId;

        using (_logger.BeginScope(new Dictionary<string, object> { ["correlationId"] = scopeCorrelationId }))
        {
            await _next(context);
        }
    }
}
