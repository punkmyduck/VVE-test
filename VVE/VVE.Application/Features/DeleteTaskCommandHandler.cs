using VVE.Application.Abstractions;
using VVE.Application.Abstractions.Persistence;
using VVE.Application.Exceptions;

namespace VVE.Application.Features;

public sealed class DeleteTaskCommandHandler : ICommandHandler<DeleteTaskCommand>
{
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteTaskCommandHandler(
        ITaskItemRepository taskItemRepository,
        IUnitOfWork unitOfWork)
    {
        _taskItemRepository = taskItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(DeleteTaskCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Id == Guid.Empty)
        {
            throw new ArgumentException("Id задачи не может быть пустым.", nameof(command.Id));
        }

        var taskItem = await _taskItemRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException($"Задача с Id {command.Id} не найдена.");

        _taskItemRepository.Delete(taskItem);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
