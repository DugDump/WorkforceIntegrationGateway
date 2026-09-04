using WorkforceIntegrationGateway.Domain.VerificationRequests;

namespace WorkforceIntegrationGateway.Application.VerificationRequests;

public sealed class VerificationRequestService(
    IVerificationRequestRepository repository,
    IVerificationRequestIdGenerator idGenerator,
    TimeProvider timeProvider)
{
    public async ValueTask<VerificationRequestCreationResult> CreateAsync(
        CreateVerificationRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = VerificationRequest.Create(
            idGenerator.Create(),
            command.ClientReference,
            command.EmployeeReference,
            command.EmployerReference,
            command.RequestedData,
            timeProvider.GetUtcNow());

        if (!result.IsSuccess)
        {
            return result;
        }

        await repository.AddAsync(result.Request!, cancellationToken);
        return result;
    }

    public ValueTask<VerificationRequest?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        repository.FindAsync(id, cancellationToken);
}
