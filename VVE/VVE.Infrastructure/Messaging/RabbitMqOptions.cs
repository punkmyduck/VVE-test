namespace VVE.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public const string ExchangeName = "task.events";
    public const string RoutingKey = "task.completed";

    public string HostName { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "vve";
    public string Password { get; set; } = "vve_dev_password";
    public string VirtualHost { get; set; } = "/";
}
