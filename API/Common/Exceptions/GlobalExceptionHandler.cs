using Shared.Common.Errors;
using Shared.Common.Results;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using System.Diagnostics;

namespace API.Common.Exceptions;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        if (exception is ValidationException validationException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            var problemDetails = new
            {
                type = CommonErrors.Validation.ValidationError.GetTypeUri(),
                title = CommonErrors.Validation.ValidationError.Message,
                status = StatusCodes.Status400BadRequest,
                errorCode = CommonErrors.Validation.ValidationError.Code,
                errors = validationException.Errors.Select(e => new
                {
                    field = ToCamelCase(e.PropertyName),
                    code = e.ErrorCode,
                    message = e.ErrorMessage,
                    attemptedValue = e.AttemptedValue,
                    severity = e.Severity.ToString().ToLowerInvariant()
                }).ToList(),
                traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier
            };

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        var (statusCode, error) = exception switch
        {
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                CommonErrors.Unauthorized
            ),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                CommonErrors.NotFound
            ),
            InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                CommonErrors.InvalidOperation with { Detail = exception.Message }
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                CommonErrors.ServerError
            )
        };

        httpContext.Response.StatusCode = statusCode;

        var errorDetails = new
        {
            type = error.GetTypeUri(),
            title = error.Message,
            status = error.StatusCode,
            errorCode = error.Code,
            detail = error.Detail,
            traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier
        };

        await httpContext.Response.WriteAsJsonAsync(errorDetails, cancellationToken);

        return true;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
            return value;

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
