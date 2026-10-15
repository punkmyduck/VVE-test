using VVE.Application.Events;

namespace VVE.Application.Abstractions.Messaging;

public interface ITaskEventPublisher
{
    // Ставит событие в очередь публикации после сохранения задачи
    void PublishTaskCompleted(TaskCompletedEvent message);
}
