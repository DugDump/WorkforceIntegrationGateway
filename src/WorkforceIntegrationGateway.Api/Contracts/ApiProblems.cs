using Microsoft.AspNetCore.Mvc;
using WorkforceIntegrationGateway.Domain.VerificationRequests;

namespace WorkforceIntegrationGateway.Api.Contracts;

internal static class ApiProblems
{
    internal static ObjectResult Validation(
        IEnumerable<VerificationRequestValidationError> errors) =>
        Validation(errors.Select(error => new ApiValidationError(error.Field, error.Code, error.Message)));

    internal static ObjectResult Validation(IEnumerable<ApiValidationError> errors) =>
        ProblemResult(
            new ApiValidationProblem(
                "urn:wig:problem:validation",
                "The request is invalid.",
                StatusCodes.Status400BadRequest,
                "validation_failed",
                errors.ToArray()),
            StatusCodes.Status400BadRequest);

    internal static ObjectResult UnsupportedMediaType() =>
        ProblemResult(
            new ApiProblem(
                "urn:wig:problem:unsupported-media-type",
                "The request content type is unsupported.",
                StatusCodes.Status415UnsupportedMediaType,
                "unsupported_media_type"),
            StatusCodes.Status415UnsupportedMediaType);

    internal static ObjectResult NotFound() =>
        ProblemResult(
            new ApiProblem(
                "urn:wig:problem:verification-request-not-found",
                "The verification request was not found.",
                StatusCodes.Status404NotFound,
                "verification_request_not_found"),
            StatusCodes.Status404NotFound);

    private static ObjectResult ProblemResult(object problem, int statusCode) => new(problem)
    {
        StatusCode = statusCode,
        ContentTypes = { "application/problem+json" }
    };
}
