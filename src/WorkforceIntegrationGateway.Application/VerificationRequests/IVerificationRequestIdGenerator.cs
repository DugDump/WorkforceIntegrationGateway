namespace WorkforceIntegrationGateway.Application.VerificationRequests;

public interface IVerificationRequestIdGenerator
{
    Guid Create();
}
