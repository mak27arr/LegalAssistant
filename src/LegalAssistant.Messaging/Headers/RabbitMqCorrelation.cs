using System.Text;

namespace LegalAssistant.Messaging;

public static class RabbitMqCorrelation
{
    public const string HeaderName = "X-Correlation-Id";

    public static string? TryGetCorrelationId(IReadOnlyDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue(HeaderName, out var raw) || raw is null)
            return null;

        return raw switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string value => value,
            _ => raw.ToString()
        };
    }

    public static void SetCorrelationId(IDictionary<string, object> headers, string correlationId) => headers[HeaderName] = correlationId;
}
