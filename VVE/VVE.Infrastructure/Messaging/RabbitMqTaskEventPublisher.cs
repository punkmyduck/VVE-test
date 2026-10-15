using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using VVE.Application.Abstractions.Messaging;
using VVE.Application.Events;

namespace VVE.Infrastructure.Messaging;

public sealed class RabbitMqTaskEventPublisher : BackgroundService, ITaskEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Channel<TaskCompletedEvent> _messages = Channel.CreateBounded<TaskCompletedEvent>(
        new BoundedChannelOptions(1024) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly CancellationTokenSource _abort = new();
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqTaskEventPublisher> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private int _disposed;

    public RabbitMqTaskEventPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqTaskEventPublisher> logger)
    {
        _logger = logger;
        var settings = options.Value;
        _connectionFactory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(1)
        };
    }

    public void PublishTaskCompleted(TaskCompletedEvent message)
    {
        // Не задерживаем HTTP-запрос, при переполнении или остановке сохраняем событие в логе
        if (!_messages.Writer.TryWrite(message))
        {
            LogUnsent(message, null);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // stoppingToken отменяется сразу при остановке, для отправки оставшихся событий используем отдельный токен
        try
        {
            await foreach (var message in _messages.Reader.ReadAllAsync(_abort.Token))
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_abort.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    await PublishAsync(message, timeout.Token);
                }
                catch (Exception exception)
                {
                    LogUnsent(message, exception);
                    await CloseConnectionAsync();
                }
            }
        }
        catch (OperationCanceledException) when (_abort.IsCancellationRequested)
        {
        }
        finally
        {
            while (_messages.Reader.TryRead(out var message))
            {
                LogUnsent(message, null);
            }

            await CloseConnectionAsync();
        }
    }

    private async Task PublishAsync(TaskCompletedEvent message, CancellationToken cancellationToken)
    {
        if (_connection is null || !_connection.IsOpen || _channel is null || !_channel.IsOpen)
        {
            await CloseConnectionAsync();
            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await _channel.ExchangeDeclareAsync(
                RabbitMqOptions.ExchangeName, ExchangeType.Topic,
                durable: true, autoDelete: false, cancellationToken: cancellationToken);
        }

        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
            MessageId = message.TaskId.ToString(),
            Type = RabbitMqOptions.RoutingKey
        };

        // Каналом пользуется только один фоновый обработчик, confirms по ТЗ не ожидаем
        await _channel.BasicPublishAsync(
            RabbitMqOptions.ExchangeName, RabbitMqOptions.RoutingKey,
            mandatory: false, basicProperties: properties, body: body,
            cancellationToken: cancellationToken);
        _logger.LogInformation("Отправлено событие {EventType} в RabbitMQ: {Message}", RabbitMqOptions.RoutingKey, JsonSerializer.Serialize(message, JsonOptions));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _messages.Writer.TryComplete();
        // Четыре секунды на очередь и ещё одна на закрытие канала и соединения
        _abort.CancelAfter(TimeSpan.FromSeconds(4));
        using var registration = cancellationToken.Register(() => _abort.Cancel());
        await base.StopAsync(cancellationToken);
    }

    private async Task CloseConnectionAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var channel = _channel;
        var connection = _connection;
        _channel = null;
        _connection = null;

        try
        {
            if (channel is not null)
            {
                try
                {
                    if (channel.IsOpen)
                    {
                        await channel.CloseAsync(cancellationToken: timeout.Token);
                    }
                }
                finally
                {
                    await channel.DisposeAsync().AsTask().WaitAsync(timeout.Token);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось корректно закрыть канал RabbitMQ.");
        }

        try
        {
            if (connection is not null)
            {
                try
                {
                    if (connection.IsOpen)
                    {
                        await connection.CloseAsync(cancellationToken: timeout.Token);
                    }
                }
                finally
                {
                    await connection.DisposeAsync().AsTask().WaitAsync(timeout.Token);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось корректно закрыть соединение RabbitMQ.");
        }
    }

    private void LogUnsent(TaskCompletedEvent message, Exception? exception)
    {
        _logger.LogWarning(exception,
            "Событие не отправлено в RabbitMQ. Exchange: {Exchange}, RoutingKey: {RoutingKey}, Message: {Message}",
            RabbitMqOptions.ExchangeName, RabbitMqOptions.RoutingKey,
            JsonSerializer.Serialize(message, JsonOptions));
    }

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _messages.Writer.TryComplete();
            _abort.Cancel();
            base.Dispose();
            _abort.Dispose();
        }
    }
}
