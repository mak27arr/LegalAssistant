using RabbitMQ.Client;

namespace LegalAssistant.Messaging;

public sealed class RabbitMqConnectionOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "rabbitmq";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public bool AutomaticRecoveryEnabled { get; set; } = true;
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

    public ConnectionFactory CreateFactory()
        => new()
        {
            HostName = Host,
            Port = Port,
            UserName = UserName,
            Password = Password,
            AutomaticRecoveryEnabled = AutomaticRecoveryEnabled,
            NetworkRecoveryInterval = ReconnectDelay,
            DispatchConsumersAsync = true
        };
}
