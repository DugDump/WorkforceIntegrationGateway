namespace WorkforceIntegrationGateway.Api.Contracts;

public sealed record ApiValidationProblem(
    string Type,
    string Title,
    int Status,
    string Code,
    IReadOnlyList<ApiValidationError> Errors);
