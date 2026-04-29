// ============================================================
// OmniSift.Api — Global Exception Handler
// Catches unhandled exceptions and returns RFC 7807 ProblemDetails
// ============================================================

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OmniSift.Api.Infrastructure;

/// <summary>
/// Centralized exception handler that converts unhandled exceptions
/// into RFC 7807 ProblemDetails responses. Replaces per-controller
/// try/catch blocks. Registered via AddExceptionHandler in Program.cs.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Let cancellation propagate naturally — do not handle
        if (exception is OperationCanceledException)
            return false;

        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var (statusCode, title) = exception switch
        {
            ArgumentException or ArgumentNullException
                => (StatusCodes.Status400BadRequest, "Bad Request"),
            InvalidOperationException
                => (StatusCodes.Status422UnprocessableEntity, "Unprocessable Request"),
            HttpRequestException
                => (StatusCodes.Status502BadGateway, "Upstream Service Error"),
            _
                => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
