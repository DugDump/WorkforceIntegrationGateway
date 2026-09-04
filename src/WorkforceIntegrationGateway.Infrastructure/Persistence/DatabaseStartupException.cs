namespace WorkforceIntegrationGateway.Infrastructure.Persistence;

public sealed class DatabaseStartupException(string message) : Exception(message);
