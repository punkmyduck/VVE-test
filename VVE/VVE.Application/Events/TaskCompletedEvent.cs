using VVE.Domain;

namespace VVE.Application.Events;

public sealed record TaskCompletedEvent(
    Guid TaskId,
    string Title,
    DateTimeOffset CompletedAt,
    Priority Priority);
