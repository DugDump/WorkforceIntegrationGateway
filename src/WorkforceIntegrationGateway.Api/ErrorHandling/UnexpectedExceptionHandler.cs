using Microsoft.AspNetCore.Diagnostics;
using WorkforceIntegrationGateway.Api.Contracts;
using WorkforceIntegrationGateway.Application.VerificationRequests;

namespace WorkforceIntegrationGateway.Api.ErrorHandling;

internal sealed class UnexpectedExceptionHandler(
    ILogger<UnexpectedExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        if (exception is PersistenceUnavailableException unavailable)
        {
            logger.LogWarning(
                "Persistence operation {Operation} is unavailable for trace {TraceIdentifier}.",
                unavailable.Operation,
                httpContext.TraceIdentifier);
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(
                new ApiProblem(
                    "urn:wig:problem:persistence-unavailable",
                    "Persistence is temporarily unavailable.",
                    StatusCodes.Status503ServiceUnavailable,
                    "persistence_unavailable"),
                options: null,
                contentType: "application/problem+json",
                cancellationToken);
            return true;
        }

        logger.LogError(
            "Unexpected verification request failure for trace {TraceIdentifier}.",
            httpContext.TraceIdentifier);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(
            new ApiProblem(
                "urn:wig:problem:unexpected",
                "An unexpected error occurred.",
                StatusCodes.Status500InternalServerError,
                "unexpected_error"),
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
        return true;
    }
}
