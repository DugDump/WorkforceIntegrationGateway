namespace WorkforceIntegrationGateway.Application.VerificationRequests;

public sealed record CreateVerificationRequestCommand(
    string? ClientReference,
    string? EmployeeReference,
    string? EmployerReference,
    IReadOnlyList<string?>? RequestedData);
