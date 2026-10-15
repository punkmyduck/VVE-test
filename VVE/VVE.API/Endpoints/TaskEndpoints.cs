using VVE.Application.Abstractions;
using VVE.Application.Features;
using VVE.Domain;

namespace VVE.API.Endpoints;

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tasks = endpoints.MapGroup("/tasks").WithTags("Задачи");

        tasks.MapPost("", async (
            CreateTaskItemCommand command,
            ICommandHandler<CreateTaskItemCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var id = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/tasks/{id}", new { id });
        })
        .WithSummary("Создать задачу")
        .Produces(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        tasks.MapGet("", async (
            IRequestHandler<GetAllTasksRequest, IReadOnlyList<TaskItem>> handler,
            CancellationToken cancellationToken) =>
        {
            var items = await handler.HandleAsync(new GetAllTasksRequest(), cancellationToken);
            return Results.Ok(items);
        })
        .WithSummary("Получить все задачи")
        .Produces<IReadOnlyList<TaskItem>>();

        tasks.MapPut("/{id}/complete", async (
            Guid id,
            ICommandHandler<CompleteTaskCommand> handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new CompleteTaskCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .WithSummary("Завершить задачу")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        tasks.MapDelete("/{id}", async (
            Guid id,
            ICommandHandler<DeleteTaskCommand> handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new DeleteTaskCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .WithSummary("Удалить задачу")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
