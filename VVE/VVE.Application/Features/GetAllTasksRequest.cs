using VVE.Application.Abstractions;
using VVE.Domain;

namespace VVE.Application.Features;

public sealed record GetAllTasksRequest : IRequest<IReadOnlyList<TaskItem>>;