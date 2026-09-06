using System.Globalization;
using System.Text;

namespace LegalAssistant.Messaging;

public static class RabbitMqRetryPolicy
{
    public const string AttemptsHeader = "x-attempts";
    public const string OriginalQueueHeader = "x-original-queue";
    public const string OriginalRoutingKeyHeader = "x-original-routing-key";
    public const string RetryExchange = "retry:dlx";

    public static int GetAttempts(IReadOnlyDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue(AttemptsHeader, out var raw) || raw is null)
            return 0;

        var text = raw switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string value => value,
            int value => value.ToString(CultureInfo.InvariantCulture),
            long value => value.ToString(CultureInfo.InvariantCulture),
            _ => raw.ToString()
        };

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var attempts)
            ? Math.Max(0, attempts)
            : 0;
    }

    public static void SetAttempts(IDictionary<string, object> headers, int attempts)
        => headers[AttemptsHeader] = attempts;

    public static int NextDelaySeconds(int attempt, RabbitMqProcessingOptions options)
    {
        if (attempt <= 1)
            return Math.Max(0, options.InitialDelaySeconds);

        var multiplier = Math.Max(0.0, options.BackoffMultiplier);
        var delay = Math.Max(0, options.InitialDelaySeconds) * Math.Pow(multiplier, attempt - 1);
        var max = Math.Max(0, options.MaxDelaySeconds);
        return (int)Math.Clamp(Math.Round(delay), 0, max);
    }

    public static string GetRetryQueueName(string destinationQueue)
        => $"retry:{destinationQueue}";

    public static RabbitMqPublishAddress GetRetryAddress(string destinationQueue)
        => new(RetryExchange, destinationQueue);
}
