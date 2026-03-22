using System.Collections.Generic;

namespace LegalAssistant.Embeddings.Messaging;

public static class RabbitMqCorrelation
{
    public const string HeaderName = "X-Correlation-Id";

    public static string? TryGetCorrelationId(IDictionary<string, object>? headers)
    {
        if (headers == null) return null;
        if (!headers.TryGetValue(HeaderName, out var raw) || raw == null) return null;

        return raw switch
        {
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            string s => s,
            _ => raw.ToString()
        };
    }

    public static void SetCorrelationId(IDictionary<string, object>? headers, string correlationId)
    {
        if (headers == null) return;
        headers[HeaderName] = correlationId;
    }
}
