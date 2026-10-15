using System.Collections.Concurrent;
using VVE.Application.Abstractions.Messaging;
using VVE.Application.Events;

namespace VVE.Tests;

public sealed class RecordingTaskEventPublisher : ITaskEventPublisher
{
    public ConcurrentQueue<TaskCompletedEvent> Messages { get; } = new();

    public void PublishTaskCompleted(TaskCompletedEvent message) => Messages.Enqueue(message);
}
