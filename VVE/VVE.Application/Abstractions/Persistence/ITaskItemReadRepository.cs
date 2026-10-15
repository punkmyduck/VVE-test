using VVE.Domain;

namespace VVE.Application.Abstractions.Persistence;

public interface ITaskItemReadRepository
{
    Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken);
}
