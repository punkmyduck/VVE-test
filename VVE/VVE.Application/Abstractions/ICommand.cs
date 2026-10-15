namespace VVE.Application.Abstractions;

/// <summary>
/// Представляет собой команду по методологии CQRS, которая может изменять состояние данных и не возвращает результат
/// </summary>
public interface ICommand
{
}

/// <summary>
/// Представляет собой команду по методологии CQRS, которая может изменять состояние данных и возвращает результат операции
/// </summary>
public interface ICommand<out TResponse>
{
}
