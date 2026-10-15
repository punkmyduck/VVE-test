namespace VVE.Application.Abstractions;

/// <summary>
/// Представляет собой обработчик команды, которая не возвращает результат
/// </summary>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Представляет собой обработчик команды, которая возвращает результат операции
/// </summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
