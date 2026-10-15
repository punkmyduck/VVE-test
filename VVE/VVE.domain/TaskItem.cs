namespace VVE.domain;

public class TaskItem
{
    public const int TitleMaxLength = 200;

    public Guid Id { get; private set; }

    public string Title { get; private set; } = null!;

    public bool IsCompleted { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Priority Priority { get; private set; }

    // Конструктор для материализации EF
    private TaskItem()
    {
    }

    public TaskItem(string title, Priority priority = Priority.Medium)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title не может быть null.", nameof(title));
        }

        if (title.Length > TitleMaxLength)
        {
            throw new ArgumentException($"Title не должен быть длиннее {TitleMaxLength} символов", nameof(title));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), priority, $"Неизвестный тип перечисления {nameof(domain.Priority)}.");
        }

        Id = Guid.NewGuid();
        Title = title;
        Priority = priority;
        CreatedAt = DateTimeOffset.UtcNow;
        IsCompleted = false;
        CompletedAt = null;
    }

    public void Complete()
    {
        if (IsCompleted)
        {
            throw new InvalidOperationException("Task уже помечен как IsCompleted.");
        }

        CompletedAt = DateTimeOffset.UtcNow;
        IsCompleted = true;
    }
}
