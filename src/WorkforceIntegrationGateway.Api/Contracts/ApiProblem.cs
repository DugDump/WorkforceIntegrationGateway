namespace WorkforceIntegrationGateway.Api.Contracts;

public sealed record ApiProblem(
    string Type,
    string Title,
    int Status,
    string Code);
