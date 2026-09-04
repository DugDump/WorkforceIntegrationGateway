namespace WorkforceIntegrationGateway.Infrastructure.Persistence;

public interface IDatabaseStartupValidator
{
    Task ValidateAsync(CancellationToken cancellationToken);
}
