using WorkforceIntegrationGateway.Application.VerificationRequests;

namespace WorkforceIntegrationGateway.Infrastructure.VerificationRequests;

public sealed class RandomVerificationRequestIdGenerator : IVerificationRequestIdGenerator
{
    public Guid Create() => Guid.NewGuid();
}
