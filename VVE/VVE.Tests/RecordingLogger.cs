using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace VVE.Tests;

public sealed class RecordingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Enqueue($"{formatter(state, exception)} {exception}");
    }
}
