namespace WorkforceIntegrationGateway.Infrastructure.Persistence;

internal sealed class VerificationRequestRecord
{
    public Guid Id { get; set; }

    public required string ClientReference { get; set; }

    public required string EmployeeReference { get; set; }

    public required string EmployerReference { get; set; }

    public required string Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public List<RequestedDataRecord> RequestedData { get; set; } = [];
}
