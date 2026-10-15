using VVE.Application.Abstractions;
using VVE.Application.Abstractions.Persistence;
using VVE.Application.Abstractions.Messaging;
using VVE.Application.Events;
using VVE.Application.Exceptions;

namespace VVE.Application.Features;

public sealed class CompleteTaskCommandHandler : ICommandHandler<CompleteTaskCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly ITaskEventPublisher _taskEventPublisher;

    public CompleteTaskCommandHandler(
        IUnitOfWork unitOfWork,
        ITaskItemRepository taskItemRepository,
        ITaskEventPublisher taskEventPublisher)
    {
        _unitOfWork = unitOfWork;
        _taskItemRepository = taskItemRepository;
        _taskEventPublisher = taskEventPublisher;
    }

    public async Task HandleAsync(CompleteTaskCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Id == Guid.Empty)
        {
            throw new ArgumentException("Id задачи не может быть пустым.", nameof(command.Id));
        }

        var taskItem = await _taskItemRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException($"Задача с Id {command.Id} не найдена.");

        if (taskItem.IsCompleted)
        {
            throw new ConflictException($"Задача с Id {command.Id} уже завершена.");
        }

        taskItem.Complete();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // HACK: я бы здесь сделал через Outbox, чтобы события вообще не
        // терялись в случае если чето упало и сохранялись в рамках одной транзакции, но уже не хватает на это времени
        _taskEventPublisher.PublishTaskCompleted(new TaskCompletedEvent(
            taskItem.Id,
            taskItem.Title,
            taskItem.CompletedAt!.Value,
            taskItem.Priority));
    }
}
