using VVE.Application.Abstractions;
using VVE.Application.Features;
using VVE.Domain;

namespace VVE.API.Endpoints;

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tasks = endpoints.MapGroup("/tasks");

        tasks.MapPost("", async (
            CreateTaskItemCommand command,
            ICommandHandler<CreateTaskItemCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var id = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/tasks/{id}", new { id });
        });

        tasks.MapGet("", async (
            IRequestHandler<GetAllTasksRequest, IReadOnlyList<TaskItem>> handler,
            CancellationToken cancellationToken) =>
        {
            var items = await handler.HandleAsync(new GetAllTasksRequest(), cancellationToken);
            return Results.Ok(items);
        });

        tasks.MapPut("/{id}/complete", async (
            Guid id,
            ICommandHandler<CompleteTaskCommand> handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new CompleteTaskCommand(id), cancellationToken);
            return Results.NoContent();
        });

        tasks.MapDelete("/{id}", async (
            Guid id,
            ICommandHandler<DeleteTaskCommand> handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new DeleteTaskCommand(id), cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }
}
