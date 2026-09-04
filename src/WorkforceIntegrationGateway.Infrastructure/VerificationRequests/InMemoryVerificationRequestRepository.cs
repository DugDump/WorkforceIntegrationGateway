using System.Collections.Concurrent;
using WorkforceIntegrationGateway.Application.VerificationRequests;
using WorkforceIntegrationGateway.Domain.VerificationRequests;

namespace WorkforceIntegrationGateway.Infrastructure.VerificationRequests;

public sealed class InMemoryVerificationRequestRepository : IVerificationRequestRepository
{
    private readonly ConcurrentDictionary<Guid, VerificationRequest> requests = new();

    public ValueTask AddAsync(VerificationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!requests.TryAdd(request.Id, request))
        {
            throw new InvalidOperationException("The generated verification-request identifier is already in use.");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<VerificationRequest?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        requests.TryGetValue(id, out var request);
        return ValueTask.FromResult(request);
    }
}
