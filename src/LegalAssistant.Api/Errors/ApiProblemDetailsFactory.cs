using System.Diagnostics;
using LegalAssistant.Core.Correlation;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Errors;

internal static class ApiProblemDetailsFactory
{
    private const string ProblemJsonContentType = "application/problem+json";

    public static ProblemDetails CreateProblemDetails(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string? type = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type ?? $"https://httpstatuses.com/{statusCode}",
            Instance = context.Request.Path
        };

        Enrich(problem, context);
        return problem;
    }

    public static ValidationProblemDetails CreateValidationProblemDetails(
        ActionContext context,
        string title = "Validation failed",
        string detail = "One or more validation errors occurred.")
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{StatusCodes.Status400BadRequest}",
            Instance = context.HttpContext.Request.Path
        };

        Enrich(problem, context.HttpContext);
        return problem;
    }

    public static void ApplyResponse(HttpContext context, ProblemDetails problem)
    {
        context.Response.ContentType = ProblemJsonContentType;
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        if (problem.Extensions.TryGetValue("correlationId", out var correlationId) &&
            correlationId is string correlationIdValue &&
            !string.IsNullOrWhiteSpace(correlationIdValue))
        {
            context.Response.Headers["X-Correlation-Id"] = correlationIdValue;
        }
    }

    private static void Enrich(ProblemDetails problem, HttpContext context)
    {
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        var correlationContext = context.RequestServices.GetService<ICorrelationContext>();
        var correlationId = string.IsNullOrWhiteSpace(correlationContext?.CorrelationId)
            ? context.Request.Headers["X-Correlation-Id"].ToString()
            : correlationContext?.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }
    }
}
