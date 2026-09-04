namespace WorkforceIntegrationGateway.Application.VerificationRequests;

public sealed class PersistenceUnavailableException : Exception
{
    public PersistenceUnavailableException(string operation, Exception innerException)
        : base($"Persistence is unavailable for operation '{operation}'.", innerException)
    {
        Operation = operation;
    }

    public string Operation { get; }
}
