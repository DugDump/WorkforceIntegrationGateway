using WorkforceIntegrationGateway.Domain.VerificationRequests;
using RequestedDataKind = WorkforceIntegrationGateway.Domain.VerificationRequests.RequestedData;

namespace WorkforceIntegrationGateway.Api.Contracts;

public sealed record VerificationRequestResponse(
    string Id,
    string ClientReference,
    string EmployeeReference,
    string EmployerReference,
    IReadOnlyList<string> RequestedData,
    string Status,
    string CreatedAtUtc)
{
    public static VerificationRequestResponse FromDomain(VerificationRequest request) => new(
        request.Id.ToString("D").ToLowerInvariant(),
        request.ClientReference,
        request.EmployeeReference,
        request.EmployerReference,
        request.RequestedData.Select(ToContractValue).ToArray(),
        "pending",
        request.CreatedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'"));

    private static string ToContractValue(RequestedDataKind requestedData) => requestedData switch
    {
        RequestedDataKind.EmploymentStatus => "employmentStatus",
        RequestedDataKind.JobTitle => "jobTitle",
        RequestedDataKind.EmploymentDates => "employmentDates",
        _ => throw new ArgumentOutOfRangeException(nameof(requestedData), requestedData, null)
    };
}
