using VVE.Application.Abstractions;

namespace VVE.Application.Features;

public sealed record DeleteTaskCommand(Guid Id) : ICommand;
