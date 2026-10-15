using Microsoft.EntityFrameworkCore;
using VVE.Application.Abstractions.Persistence;
using VVE.Domain;

namespace VVE.Infrastructure.Persistence.Repositories;

public sealed class TaskItemReadRepository : ITaskItemReadRepository
{
    private readonly TaskDbContext _context;

    public TaskItemReadRepository(
        TaskDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _context.Tasks.AsNoTracking().ToListAsync(cancellationToken)).AsReadOnly();
    }
}
