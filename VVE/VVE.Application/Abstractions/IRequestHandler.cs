namespace VVE.Application.Abstractions;

/// <summary>
/// Представляет собой обработчик запроса на чтение данных, который всегда возвращает результат
/// </summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
