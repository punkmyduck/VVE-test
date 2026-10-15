using VVE.Application.Abstractions;

namespace VVE.Application.Features;

public sealed record CompleteTaskCommand(Guid Id) : ICommand;
