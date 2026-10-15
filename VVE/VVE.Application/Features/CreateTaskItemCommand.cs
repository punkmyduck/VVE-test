using VVE.Application.Abstractions;
using VVE.Domain;

namespace VVE.Application.Features;

public sealed record CreateTaskItemCommand(
    string Title,
    Priority Priority = Priority.Medium) : ICommand<Guid>;