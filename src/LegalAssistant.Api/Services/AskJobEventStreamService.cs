using System.Globalization;
using System.Text.Json;
using LegalAssistant.Api.Mappers;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Domain.Models;
using Microsoft.AspNetCore.Http;

namespace LegalAssistant.Api.Services;

public sealed class AskJobEventStreamService : IAskJobEventStreamService
{
    private readonly IAskJobEventQueryService _events;
    private readonly IAskJobEventFanout _fanout;

    public AskJobEventStreamService(IAskJobEventQueryService events, IAskJobEventFanout fanout)
    {
        _events = events;
        _fanout = fanout;
    }

    public async Task StreamAsync(Guid jobId, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var response = httpContext.Response;
        var lastEventId = ParseLastEventId(httpContext.Request.Headers["Last-Event-ID"]);

        var latest = await _events.GetLatestAsync(jobId, cancellationToken);
        if (latest == null)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            response.ContentType = "application/problem+json";
            await response.WriteAsync("{\"message\":\"job not found\"}", cancellationToken);
            return;
        }

        response.StatusCode = StatusCodes.Status200OK;
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["Connection"] = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";
        response.ContentType = "text/event-stream";

        var subscription = _fanout.Subscribe(jobId);
        await using (subscription)
        {
            var replay = await _events.GetSinceAsync(jobId, lastEventId, cancellationToken);
            var lastSentEventId = lastEventId;

            foreach (var eventRecord in replay)
            {
                await WriteEventAsync(response, eventRecord, cancellationToken);
                lastSentEventId = eventRecord.Id;
            }

            if (replay.Count > 0 && replay[^1].Status.IsTerminal())
                return;

            if (replay.Count == 0 && latest.Status.IsTerminal() && latest.Id <= lastEventId)
                return;

            while (!cancellationToken.IsCancellationRequested)
            {
                var eventRecord = await subscription.Reader.ReadAsync(cancellationToken);
                if (eventRecord.Id <= lastSentEventId)
                    continue;

                await WriteEventAsync(response, eventRecord, cancellationToken);
                lastSentEventId = eventRecord.Id;

                if (eventRecord.Status.IsTerminal())
                    break;
            }
        }
    }

    private static async Task WriteEventAsync(HttpResponse response, AskJobEventRecord eventRecord, CancellationToken cancellationToken)
    {
        var payload = AskResponseMapper.Map(eventRecord);
        var json = JsonSerializer.Serialize(payload);
        await response.WriteAsync($"id: {eventRecord.Id}\n", cancellationToken);
        await response.WriteAsync($"event: {eventRecord.Status.ToString().ToLowerInvariant()}\n", cancellationToken);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static long ParseLastEventId(string? raw)
        => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
