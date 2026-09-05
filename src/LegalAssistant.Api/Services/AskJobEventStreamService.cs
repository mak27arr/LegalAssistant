using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using LegalAssistant.Api.Mappers;
using LegalAssistant.Api.Services.Auth;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Ask.Services;
using LegalAssistant.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Api.Services;

public sealed class AskJobEventStreamService : IAskJobEventStreamService
{
    private readonly IAskJobEventStreamUseCase _streamUseCase;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public AskJobEventStreamService(
        IAskJobEventStreamUseCase streamUseCase,
        IOptions<JsonOptions> jsonOptions)
    {
        _streamUseCase = streamUseCase;
        _jsonSerializerOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    public async Task StreamAsync(Guid jobId, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var response = httpContext.Response;
        var lastEventId = ParseLastEventId(httpContext.Request.Headers["Last-Event-ID"]);
        var ownerUserId = httpContext.User.ToAuthenticatedUser().Id;
        var sessionId = httpContext.User.FindFirstValue(ApplicationAuthSchemes.SessionIdClaimType);

        var isHeadersConfigured = false;

        await foreach (var item in _streamUseCase.StreamEventsAsync(jobId, ownerUserId, sessionId, lastEventId, cancellationToken))
        {
            switch (item.Kind)
            {
                case AskJobStreamItemKind.JobNotFound:
                    response.StatusCode = StatusCodes.Status404NotFound;
                    response.ContentType = "application/problem+json";
                    await response.WriteAsync("{\"message\":\"job not found\"}", cancellationToken);
                    return;

                case AskJobStreamItemKind.SessionExpired:
                    return;

                case AskJobStreamItemKind.Heartbeat:
                    EnsureSseHeadersConfigured(response, ref isHeadersConfigured);
                    await response.WriteAsync(": keep-alive\n\n", cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                    break;

                case AskJobStreamItemKind.Event:
                    if (item.EventRecord != null)
                    {
                        EnsureSseHeadersConfigured(response, ref isHeadersConfigured);
                        await WriteEventAsync(response, item.EventRecord, cancellationToken);
                    }
                    break;
            }
        }
    }

    private static void EnsureSseHeadersConfigured(HttpResponse response, ref bool isHeadersConfigured)
    {
        if (isHeadersConfigured)
            return;

        response.StatusCode = StatusCodes.Status200OK;
        response.Headers["Cache-Control"] = "no-cache, no-store";
        response.Headers["Connection"] = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.ContentType = "text/event-stream";
        isHeadersConfigured = true;
    }

    private async Task WriteEventAsync(HttpResponse response, AskJobEventRecord eventRecord, CancellationToken cancellationToken)
    {
        var payload = AskResponseMapper.Map(eventRecord);
        var json = JsonSerializer.Serialize(payload, _jsonSerializerOptions);
        await response.WriteAsync($"id: {eventRecord.Id}\n", cancellationToken);
        await response.WriteAsync($"event: {eventRecord.Status.ToString().ToLowerInvariant()}\n", cancellationToken);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static long ParseLastEventId(string? raw)
        => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
