namespace WorkforceIntegrationGateway.Domain.VerificationRequests;

public sealed record VerificationRequestValidationError(
    string Field,
    string Code,
    string Message);
