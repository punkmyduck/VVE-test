using VVE.Application.Abstractions;
using VVE.Application.Abstractions.Persistence;
using VVE.Domain;

namespace VVE.Application.Features;

public sealed class GetAllTasksRequestHandler : IRequestHandler<GetAllTasksRequest, IReadOnlyList<TaskItem>>
{
    private readonly ITaskItemReadRepository _taskItemReadRepository;

    public GetAllTasksRequestHandler(
        ITaskItemReadRepository taskItemReadRepository)
    {
        _taskItemReadRepository = taskItemReadRepository;
    }

    public Task<IReadOnlyList<TaskItem>> HandleAsync(GetAllTasksRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _taskItemReadRepository.GetAllAsync(cancellationToken);
    }
}
