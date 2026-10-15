using Microsoft.EntityFrameworkCore;
using VVE.Infrastructure.Persistence;

namespace VVE.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Postgres.");

        builder.Services.AddDbContext<TaskDbContext>(options => options.UseNpgsql(connectionString));

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

        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        app.MapGet("/weatherforecast", (HttpContext httpContext) =>
        {

        })
        .WithName("GetWeatherForecast");

        app.Run();
    }
}
