namespace WorkforceIntegrationGateway.Api.Contracts;

public sealed record ApiValidationError(
    string Field,
    string Code,
    string Message);
