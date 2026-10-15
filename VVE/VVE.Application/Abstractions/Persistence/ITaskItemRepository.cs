using VVE.Domain;

namespace VVE.Application.Abstractions.Persistence;

public interface ITaskItemRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    void Add(TaskItem item);
    void Delete(TaskItem item);
}
