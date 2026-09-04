using Microsoft.EntityFrameworkCore;

namespace WorkforceIntegrationGateway.Infrastructure.Persistence;

public sealed class DatabaseStartupValidator(GatewayDbContext dbContext) : IDatabaseStartupValidator
{
    public async Task ValidateAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                throw new DatabaseStartupException(
                    "The WorkforceGateway database is unavailable. Verify PostgreSQL is healthy and the configured endpoint is reachable.");
            }

            var known = dbContext.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var applied = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
            var unknown = applied.Where(migration => !known.Contains(migration)).ToArray();

            if (unknown.Length > 0)
            {
                throw new DatabaseStartupException(
                    "The WorkforceGateway database contains migrations unknown to this application version.");
            }

            if (known.Except(applied, StringComparer.Ordinal).Any())
            {
                throw new DatabaseStartupException(
                    "The WorkforceGateway database schema is not current. Apply the committed migrations before starting the API.");
            }
        }
        catch (DatabaseStartupException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new DatabaseStartupException(
                "The WorkforceGateway database could not be validated. Verify connectivity and migration history.");
        }
    }
}
