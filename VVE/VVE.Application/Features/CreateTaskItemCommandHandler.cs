using VVE.Application.Abstractions;
using VVE.Application.Abstractions.Persistence;
using VVE.Domain;

namespace VVE.Application.Features;

public sealed class CreateTaskItemCommandHandler : ICommandHandler<CreateTaskItemCommand, Guid>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITaskItemRepository _taskItemRepository;

    public CreateTaskItemCommandHandler(
        ITaskItemRepository taskItemRepository,
        IUnitOfWork unitOfWork)
    {
        _taskItemRepository = taskItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> HandleAsync(CreateTaskItemCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var taskItem = new TaskItem(command.Title, command.Priority);

        _taskItemRepository.Add(taskItem);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return taskItem.Id;
    }
}
