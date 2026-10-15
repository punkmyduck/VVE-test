using Microsoft.AspNetCore.Diagnostics;
using VVE.Application.Exceptions;

namespace VVE.API.ErrorHandling;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            ArgumentException => StatusCodes.Status400BadRequest,
            BadHttpRequestException badRequest => badRequest.StatusCode,
            _ => (int?)null
        };

        if (statusCode is null)
        {
            return false;
        }

        await Results.Problem(statusCode: statusCode.Value, detail: exception.Message)
            .ExecuteAsync(httpContext);

        return true;
    }
}
