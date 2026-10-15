using VVE.Application.Abstractions;
using VVE.Application.Abstractions.Persistence;
using VVE.Application.Exceptions;

namespace VVE.Application.Features;

public sealed class CompleteTaskCommandHandler : ICommandHandler<CompleteTaskCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITaskItemRepository _taskItemRepository;

    public CompleteTaskCommandHandler(
        IUnitOfWork unitOfWork,
        ITaskItemRepository taskItemRepository)
    {
        _unitOfWork = unitOfWork;
        _taskItemRepository = taskItemRepository;
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
    }
}
