using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VVE.API;
using VVE.Application.Abstractions.Messaging;
using VVE.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using VVE.Infrastructure.Persistence;

namespace VVE.Tests;

public sealed class TaskApiFactory(bool useUnavailableRabbitMq = false) : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    public RecordingTaskEventPublisher Publisher { get; } = new();
    public RecordingLogger<RabbitMqTaskEventPublisher> PublisherLogger { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TaskDbContext>();
            services.RemoveAll<DbContextOptions<TaskDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TaskDbContext>>();
            services.AddDbContext<TaskDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            if (useUnavailableRabbitMq)
            {
                services.Configure<RabbitMqOptions>(options =>
                {
                    options.HostName = "127.0.0.1";
                    options.Port = 1;
                });
                services.AddSingleton<ILogger<RabbitMqTaskEventPublisher>>(PublisherLogger);
            }
            else
            {
                services.RemoveAll<ITaskEventPublisher>();
                services.AddSingleton<ITaskEventPublisher>(Publisher);
            }
        });
    }
}
