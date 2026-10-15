using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using VVE.API;
using VVE.Application.Events;
using VVE.Domain;
using VVE.Infrastructure.Messaging;
using VVE.Infrastructure.Persistence;
using Xunit;

namespace VVE.Tests;

public sealed class RabbitMqIntegrationFactAttribute : FactAttribute
{
    public RabbitMqIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_RABBITMQ_TESTS") != "1")
        {
            Skip = "Нужны PostgreSQL и RabbitMQ из docker-compose. Для запуска: RUN_RABBITMQ_TESTS=1.";
        }
    }
}

public class RabbitMqIntegrationTests
{
    [RabbitMqIntegrationFact]
    public async Task CreateAndCompleteTask_PublishesEventToRabbitMq_AfterSavingInPostgres()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        var databaseName = $"vve_test_{Guid.NewGuid():N}";
        var adminConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES")
            ?? "Host=127.0.0.1;Port=5432;Database=vve_tasks;Username=vve;Password=vve_dev_password";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(token);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
        {
            await create.ExecuteNonQueryAsync(token);
        }

        try
        {
            var connectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = databaseName,
                Pooling = false
            }.ConnectionString;

            await using var connection = await CreateConnectionFactory().CreateConnectionAsync(token);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: token);
            var queue = await BindTestQueue(channel, token);

            await using var factory = new PostgresApiFactory(connectionString);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<TaskDbContext>().Database.MigrateAsync(token);
            }

            var created = await client.PostAsJsonAsync("/tasks", new { Title = "Купить молоко", Priority = "High" }, token);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetGuid();

            var response = await client.PutAsync($"/tasks/{id}/complete", null, token);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            BasicGetResult? delivery = null;
            while (delivery is null)
            {
                delivery = await channel.BasicGetAsync(queue, autoAck: true, cancellationToken: token);
                if (delivery is null)
                {
                    await Task.Delay(50, token);
                }
            }

            using var json = JsonDocument.Parse(delivery.Body);
            var message = json.RootElement;
            Assert.Equal(RabbitMqOptions.ExchangeName, delivery.Exchange);
            Assert.Equal(RabbitMqOptions.RoutingKey, delivery.RoutingKey);
            Assert.Equal(4, message.EnumerateObject().Count());
            Assert.Equal(id, message.GetProperty("taskId").GetGuid());
            Assert.Equal("Купить молоко", message.GetProperty("title").GetString());
            Assert.Equal("High", message.GetProperty("priority").GetString());
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
                var saved = await context.Tasks.AsNoTracking().SingleAsync(item => item.Id == id, token);
                Assert.True(saved.IsCompleted);
                // PostgreSQL хранит микросекунды, DateTimeOffset может содержать ещё доли микросекунды
                Assert.NotNull(saved.CompletedAt);
                Assert.InRange(message.GetProperty("completedAt").GetDateTimeOffset() - saved.CompletedAt.Value,
                    TimeSpan.Zero, TimeSpan.FromTicks(9));
            }

            Assert.Equal(HttpStatusCode.Conflict,
                (await client.PutAsync($"/tasks/{id}/complete", null, token)).StatusCode);
            // После остановки фонового сервиса новых публикаций уже не будет
            await factory.DisposeAsync();
            Assert.Null(await channel.BasicGetAsync(queue, autoAck: true, cancellationToken: token));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    [RabbitMqIntegrationFact]
    public async Task Shutdown_DrainsQueuedEvents_AndClosesConnection()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var connection = await CreateConnectionFactory().CreateConnectionAsync(timeout.Token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: timeout.Token);
        var queue = await BindTestQueue(channel, timeout.Token);
        var logger = new RecordingLogger<RabbitMqTaskEventPublisher>();
        using var publisher = new RabbitMqTaskEventPublisher(Options.Create(CreateOptions()), logger);
        await publisher.StartAsync(timeout.Token);
        var ids = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToHashSet();
        foreach (var id in ids)
        {
            publisher.PublishTaskCompleted(new TaskCompletedEvent(id, "Задача", DateTimeOffset.UtcNow, Priority.High));
        }

        var stopwatch = Stopwatch.StartNew();
        await publisher.StopAsync(timeout.Token);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.True(logger.Messages.IsEmpty, string.Join(Environment.NewLine, logger.Messages));

        var receivedIds = new HashSet<Guid>();
        while (receivedIds.Count < ids.Count)
        {
            var delivery = await channel.BasicGetAsync(queue, autoAck: true, cancellationToken: timeout.Token);
            if (delivery is null)
            {
                await Task.Delay(50, timeout.Token);
                continue;
            }

            using var json = JsonDocument.Parse(delivery.Body);
            receivedIds.Add(json.RootElement.GetProperty("taskId").GetGuid());
        }

        Assert.True(ids.SetEquals(receivedIds));
    }

    private static async Task<string> BindTestQueue(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(RabbitMqOptions.ExchangeName, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);
        var queue = await channel.QueueDeclareAsync(queue: "", durable: false, exclusive: true,
            autoDelete: true, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue.QueueName, RabbitMqOptions.ExchangeName, RabbitMqOptions.RoutingKey,
            cancellationToken: cancellationToken);
        return queue.QueueName;
    }

    private static RabbitMqOptions CreateOptions() => new()
    {
        HostName = Environment.GetEnvironmentVariable("TEST_RABBITMQ_HOST") ?? "127.0.0.1",
        Port = int.Parse(Environment.GetEnvironmentVariable("TEST_RABBITMQ_PORT") ?? "5672"),
        UserName = Environment.GetEnvironmentVariable("TEST_RABBITMQ_USER") ?? "vve",
        Password = Environment.GetEnvironmentVariable("TEST_RABBITMQ_PASSWORD") ?? "vve_dev_password"
    };

    private static ConnectionFactory CreateConnectionFactory()
    {
        var options = CreateOptions();
        return new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            AutomaticRecoveryEnabled = false
        };
    }

    private sealed class PostgresApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            var options = CreateOptions();
            builder.UseSetting("RabbitMq:HostName", options.HostName);
            builder.UseSetting("RabbitMq:Port", options.Port.ToString());
            builder.UseSetting("RabbitMq:UserName", options.UserName);
            builder.UseSetting("RabbitMq:Password", options.Password);
        }
    }
}
