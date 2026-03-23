using System;
using System.Collections.Generic;

namespace LegalAssistant.Infrastructure.Messaging;

public static class RabbitMqRetryPolicy
{
    public const string AttemptsHeader = "x-attempts";

    public static int GetAttempts(IDictionary<string, object>? headers)
    {
        if (headers == null) return 0;
        if (!headers.TryGetValue(AttemptsHeader, out var raw) || raw == null) return 0;

        return raw switch
        {
            byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out var i) => i,
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var i) => i,
            _ => 0
        };
    }

    public static void SetAttempts(IDictionary<string, object>? headers, int attempts)
    {
        if (headers == null) return;
        headers[AttemptsHeader] = attempts;
    }

    public static int NextDelaySeconds(int attempt, RabbitMqProcessingOptions options)
    {
        if (attempt <= 1) return options.InitialDelaySeconds;

        var delay = options.InitialDelaySeconds * Math.Pow(options.BackoffMultiplier, attempt - 1);
        if (delay > options.MaxDelaySeconds) delay = options.MaxDelaySeconds;
        return (int)Math.Max(0, Math.Round(delay));
    }
}
