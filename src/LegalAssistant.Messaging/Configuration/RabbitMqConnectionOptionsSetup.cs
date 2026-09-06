using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Messaging;

internal sealed class RabbitMqConnectionOptionsSetup : IConfigureOptions<RabbitMqConnectionOptions>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqConnectionOptionsSetup> _logger;

    public RabbitMqConnectionOptionsSetup(
        IConfiguration configuration,
        ILogger<RabbitMqConnectionOptionsSetup> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void Configure(RabbitMqConnectionOptions options)
    {
        options.Host = ReadString(options.Host, "RabbitMq:Host", "RABBITMQ_HOST");
        options.Port = ReadPositiveInt(options.Port, "RabbitMq:Port", "RABBITMQ_PORT");
        options.UserName = ReadString(options.UserName, "RabbitMq:User", "RABBITMQ_USER");
        options.Password = ReadString(options.Password, "RabbitMq:Pass", "RABBITMQ_PASS", secret: true);
        options.AutomaticRecoveryEnabled = ReadBool(
            options.AutomaticRecoveryEnabled,
            "RabbitMq:AutomaticRecoveryEnabled",
            "RABBITMQ_AUTOMATIC_RECOVERY_ENABLED");
        options.ReconnectDelay = TimeSpan.FromSeconds(ReadPositiveInt(
            (int)options.ReconnectDelay.TotalSeconds,
            "RabbitMq:ReconnectDelaySeconds",
            "RABBITMQ_RECONNECT_DELAY_SECONDS"));
    }

    private string ReadString(string fallback, string configurationKey, string environmentKey, bool secret = false)
    {
        var value = ReadValue(configurationKey, environmentKey);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        LogDefaultUsed(configurationKey, environmentKey, secret: secret);
        return fallback;
    }

    private int ReadPositiveInt(int fallback, string configurationKey, string environmentKey)
    {
        var value = ReadValue(configurationKey, environmentKey);
        if (int.TryParse(value, out var parsed) && parsed > 0)
            return parsed;

        LogDefaultUsed(configurationKey, environmentKey, invalidValue: !string.IsNullOrWhiteSpace(value));
        return fallback;
    }

    private bool ReadBool(bool fallback, string configurationKey, string environmentKey)
    {
        var value = ReadValue(configurationKey, environmentKey);
        if (bool.TryParse(value, out var parsed))
            return parsed;

        LogDefaultUsed(configurationKey, environmentKey, invalidValue: !string.IsNullOrWhiteSpace(value));
        return fallback;
    }

    private string? ReadValue(string configurationKey, string environmentKey)
    {
        var configured = _configuration[configurationKey];
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Environment.GetEnvironmentVariable(environmentKey);
    }

    private void LogDefaultUsed(
        string configurationKey,
        string environmentKey,
        bool invalidValue = false,
        bool secret = false)
    {
        var reason = invalidValue ? "missing or invalid" : "missing";
        var valueNote = secret ? " The default value is not logged." : string.Empty;

        _logger.LogWarning(
            "RabbitMQ setting {ConfigurationKey} / {EnvironmentKey} is {Reason}; using the code default.{ValueNote}",
            configurationKey,
            environmentKey,
            reason,
            valueNote);
    }
}
