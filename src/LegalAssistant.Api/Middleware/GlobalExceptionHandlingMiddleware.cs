using System.Text.Json;
using LegalAssistant.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;

namespace LegalAssistant.Api.Middleware;

public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Validation failed",
                ex.Message,
                ex,
                logAsError: false);
        }
        catch (FormatException ex)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid input",
                ex.Message,
                ex,
                logAsError: false);
        }
        catch (BadHttpRequestException ex)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Bad request",
                "The request could not be processed.",
                ex,
                logAsError: false);
        }
        catch (JsonException ex)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid JSON",
                "The request body is not valid JSON.",
                ex,
                logAsError: false);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Unexpected server error",
                "An unexpected error occurred.",
                ex,
                logAsError: true);
        }
    }

    private async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        Exception exception,
        bool logAsError)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                exception,
                "Unable to write error response because the HTTP response has already started. Method={Method} Path={Path}",
                context.Request.Method,
                context.Request.Path);
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        var problem = ApiProblemDetailsFactory.CreateProblemDetails(context, statusCode, title, detail);

        if (logAsError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. TraceId={TraceId} CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                problem.Extensions.TryGetValue("traceId", out var traceId) ? traceId : null,
                problem.Extensions.TryGetValue("correlationId", out var correlationId) ? correlationId : null);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Request rejected for {Method} {Path}. TraceId={TraceId} CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                problem.Extensions.TryGetValue("traceId", out var traceId) ? traceId : null,
                problem.Extensions.TryGetValue("correlationId", out var correlationId) ? correlationId : null);
        }

        ApiProblemDetailsFactory.ApplyResponse(context, problem);
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
