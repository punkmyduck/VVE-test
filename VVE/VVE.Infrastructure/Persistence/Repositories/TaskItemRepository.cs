using VVE.Application.Abstractions.Persistence;
using VVE.Domain;

namespace VVE.Infrastructure.Persistence.Repositories;

public sealed class TaskItemRepository : ITaskItemRepository
{
    private readonly TaskDbContext _context;
    public TaskItemRepository(
        TaskDbContext context)
    {
        _context = context;
    }

    public void Add(TaskItem item)
    {
        _context.Tasks.Add(item);
    }

    public void Delete(TaskItem item)
    {
        _context.Tasks.Remove(item);
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Tasks.FindAsync([id], cancellationToken);
    }
}
