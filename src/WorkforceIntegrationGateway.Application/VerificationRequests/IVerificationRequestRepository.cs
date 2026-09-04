using WorkforceIntegrationGateway.Domain.VerificationRequests;

namespace WorkforceIntegrationGateway.Application.VerificationRequests;

public interface IVerificationRequestRepository
{
    ValueTask AddAsync(VerificationRequest request, CancellationToken cancellationToken);

    ValueTask<VerificationRequest?> FindAsync(Guid id, CancellationToken cancellationToken);
}
