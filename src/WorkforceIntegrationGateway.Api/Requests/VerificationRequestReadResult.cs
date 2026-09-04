using WorkforceIntegrationGateway.Api.Contracts;
using WorkforceIntegrationGateway.Application.VerificationRequests;

namespace WorkforceIntegrationGateway.Api.Requests;

internal sealed record VerificationRequestReadResult(
    CreateVerificationRequestCommand? Command,
    IReadOnlyList<ApiValidationError> Errors);
