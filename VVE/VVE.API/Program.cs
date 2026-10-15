using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using VVE.API.Endpoints;
using VVE.API.ErrorHandling;
using VVE.Application.Abstractions;
using VVE.Application.Abstractions.Messaging;
using VVE.Application.Abstractions.Persistence;
using VVE.Application.Features;
using VVE.Domain;
using VVE.Infrastructure.Persistence;
using VVE.Infrastructure.Persistence.Repositories;
using VVE.Infrastructure.Messaging;

namespace VVE.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Postgres.");

        builder.Services.AddDbContext<TaskDbContext>(options => options.UseNpgsql(connectionString));
        builder.Services.AddScoped<IUnitOfWork>(services => services.GetRequiredService<TaskDbContext>());
        builder.Services.AddScoped<ITaskItemReadRepository, TaskItemReadRepository>();
        builder.Services.AddScoped<ITaskItemRepository, TaskItemRepository>();

        builder.Services.AddScoped<
            ICommandHandler<CreateTaskItemCommand, Guid>,
            CreateTaskItemCommandHandler>();

        builder.Services.AddScoped<
            IRequestHandler<GetAllTasksRequest, IReadOnlyList<TaskItem>>,
            GetAllTasksRequestHandler>();

        builder.Services.AddScoped<ICommandHandler<CompleteTaskCommand>, CompleteTaskCommandHandler>();
        builder.Services.AddScoped<ICommandHandler<DeleteTaskCommand>, DeleteTaskCommandHandler>();

        builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
        builder.Services.AddSingleton<RabbitMqTaskEventPublisher>();
        builder.Services.AddSingleton<ITaskEventPublisher>(services =>
            services.GetRequiredService<RabbitMqTaskEventPublisher>());
        builder.Services.AddHostedService(services => services.GetRequiredService<RabbitMqTaskEventPublisher>());
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddAuthorization();

        builder.Services.AddOpenApi();

        var app = builder.Build();

        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapTaskEndpoints();

        app.Run();
    }
}
