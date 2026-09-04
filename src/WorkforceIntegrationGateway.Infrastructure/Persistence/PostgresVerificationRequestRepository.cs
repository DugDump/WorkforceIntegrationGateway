using Microsoft.EntityFrameworkCore;
using Npgsql;
using WorkforceIntegrationGateway.Application.VerificationRequests;
using WorkforceIntegrationGateway.Domain.VerificationRequests;

namespace WorkforceIntegrationGateway.Infrastructure.Persistence;

public sealed class PostgresVerificationRequestRepository(GatewayDbContext dbContext)
    : IVerificationRequestRepository
{
    public async ValueTask AddAsync(VerificationRequest request, CancellationToken cancellationToken)
    {
        var record = new VerificationRequestRecord
        {
            Id = request.Id,
            ClientReference = request.ClientReference,
            EmployeeReference = request.EmployeeReference,
            EmployerReference = request.EmployerReference,
            Status = request.Status.ToString(),
            CreatedAtUtc = request.CreatedAtUtc.UtcDateTime,
            RequestedData = request.RequestedData.Select((value, ordinal) => new RequestedDataRecord
            {
                VerificationRequestId = request.Id,
                Value = value.ToString(),
                Ordinal = ordinal
            }).ToList()
        };

        dbContext.VerificationRequests.Add(record);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (IsAvailabilityFailure(exception, cancellationToken))
        {
            throw new PersistenceUnavailableException("create", exception);
        }
    }

    public async ValueTask<VerificationRequest?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        VerificationRequestRecord? record;

        try
        {
            record = await dbContext.VerificationRequests
                .AsNoTracking()
                .Include(item => item.RequestedData)
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        }
        catch (Exception exception) when (IsAvailabilityFailure(exception, cancellationToken))
        {
            throw new PersistenceUnavailableException("find", exception);
        }

        if (record is null)
        {
            return null;
        }

        var creation = VerificationRequest.Create(
            record.Id,
            record.ClientReference,
            record.EmployeeReference,
            record.EmployerReference,
            record.RequestedData.OrderBy(item => item.Ordinal).Select(item => item.Value),
            new DateTimeOffset(DateTime.SpecifyKind(record.CreatedAtUtc, DateTimeKind.Utc)));

        return creation.Request
            ?? throw new InvalidOperationException("Persisted verification request violates domain invariants.");
    }

    private static bool IsAvailabilityFailure(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || exception is OperationCanceledException)
        {
            return false;
        }

        return exception switch
        {
            NpgsqlException npgsqlException => npgsqlException.IsTransient,
            TimeoutException => true,
            _ when exception.InnerException is not null =>
                IsAvailabilityFailure(exception.InnerException, cancellationToken),
            _ => false
        };
    }
}
