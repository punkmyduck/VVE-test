using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VVE.Application.Exceptions;
using VVE.Application.Abstractions.Persistence;
using VVE.Application.Features;
using VVE.Domain;
using VVE.Infrastructure.Persistence;
using VVE.Infrastructure.Persistence.Repositories;
using Xunit;

namespace VVE.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task FailedSave_DoesNotPublishEvent_OrPersistCompletion()
    {
        var options = new DbContextOptionsBuilder<TaskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new TaskDbContext(options);
        var task = new TaskItem("Задача");
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        var publisher = new RecordingTaskEventPublisher();
        var handler = new CompleteTaskCommandHandler(new FailingUnitOfWork(), new TaskItemRepository(context), publisher);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new CompleteTaskCommand(task.Id), CancellationToken.None));

        Assert.Empty(publisher.Messages);
        await using var verificationContext = new TaskDbContext(options);
        var saved = await verificationContext.Tasks.SingleAsync(item => item.Id == task.Id);
        Assert.False(saved.IsCompleted);
        Assert.Null(saved.CompletedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleTask_ProducesConflict_WithoutOverwritingSavedCompletion(bool delete)
    {
        var options = new DbContextOptionsBuilder<TaskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
            .Options;

        await using var firstContext = new TaskDbContext(options);
        var task = new TaskItem("Задача");
        firstContext.Tasks.Add(task);
        await firstContext.SaveChangesAsync();

        await using var secondContext = new TaskDbContext(options);
        await secondContext.Tasks.SingleAsync(item => item.Id == task.Id);

        // InMemory не генерирует xmin, поэтому имитируем новую версию строки вручную
        firstContext.Entry(task).Property<uint>("RowVersion").CurrentValue = 1;
        var publisher = new RecordingTaskEventPublisher();
        var firstHandler = new CompleteTaskCommandHandler(firstContext, new TaskItemRepository(firstContext), publisher);
        await firstHandler.HandleAsync(new CompleteTaskCommand(task.Id), CancellationToken.None);
        var completedAt = task.CompletedAt;

        var repository = new TaskItemRepository(secondContext);
        Task ExecuteStaleCommand() => delete
            ? new DeleteTaskCommandHandler(repository, secondContext)
                .HandleAsync(new DeleteTaskCommand(task.Id), CancellationToken.None)
            : new CompleteTaskCommandHandler(secondContext, repository, publisher)
                .HandleAsync(new CompleteTaskCommand(task.Id), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(ExecuteStaleCommand);
        Assert.IsType<DbUpdateConcurrencyException>(exception.InnerException);
        Assert.Single(publisher.Messages);

        await using var verificationContext = new TaskDbContext(options);
        var savedTask = await verificationContext.Tasks.SingleAsync(item => item.Id == task.Id);
        Assert.True(savedTask.IsCompleted);
        Assert.Equal(completedAt, savedTask.CompletedAt);
    }

    [Fact]
    public async Task ReadRepository_DoesNotTrackTasks()
    {
        var options = new DbContextOptionsBuilder<TaskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TaskDbContext(options);
        context.Tasks.Add(new TaskItem("Задача"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new GetAllTasksRequestHandler(new TaskItemReadRepository(context));
        var tasks = await handler.HandleAsync(new GetAllTasksRequest(), CancellationToken.None);

        Assert.Single(tasks);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Ошибка сохранения БД.");
    }
}
