namespace VVE.Application.Abstractions;

/// <summary>
/// Представляет собой запрос по методологии CQRS, который нужен только для чтения данных и всегда возвращает результат
/// </summary>
public interface IRequest<out TResponse>
{
}
