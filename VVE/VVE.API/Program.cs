using Microsoft.EntityFrameworkCore;
using VVE.Application.Abstractions.Persistence;
using VVE.Infrastructure.Persistence;
using VVE.Infrastructure.Persistence.Repositories;

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

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.Run();
    }
}
