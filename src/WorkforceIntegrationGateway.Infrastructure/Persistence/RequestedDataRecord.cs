namespace WorkforceIntegrationGateway.Infrastructure.Persistence;

internal sealed class RequestedDataRecord
{
    public Guid VerificationRequestId { get; set; }

    public required string Value { get; set; }

    public int Ordinal { get; set; }

    public VerificationRequestRecord VerificationRequest { get; set; } = null!;
}
