using System.Collections.ObjectModel;
using RequestedDataKind = WorkforceIntegrationGateway.Domain.VerificationRequests.RequestedData;

namespace WorkforceIntegrationGateway.Domain.VerificationRequests;

public sealed class VerificationRequest
{
    private const int MaximumReferenceLength = 100;

    private static readonly IReadOnlyDictionary<string, RequestedData> RequestedDataByName =
        new Dictionary<string, RequestedData>(StringComparer.Ordinal)
        {
            [nameof(RequestedDataKind.EmploymentStatus)] = RequestedDataKind.EmploymentStatus,
            [nameof(RequestedDataKind.JobTitle)] = RequestedDataKind.JobTitle,
            [nameof(RequestedDataKind.EmploymentDates)] = RequestedDataKind.EmploymentDates
        };

    private VerificationRequest(
        Guid id,
        string clientReference,
        string employeeReference,
        string employerReference,
        IReadOnlyList<RequestedData> requestedData,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ClientReference = clientReference;
        EmployeeReference = employeeReference;
        EmployerReference = employerReference;
        RequestedData = requestedData;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public string ClientReference { get; }

    public string EmployeeReference { get; }

    public string EmployerReference { get; }

    public IReadOnlyList<RequestedData> RequestedData { get; }

    public VerificationRequestStatus Status => VerificationRequestStatus.Pending;

    public DateTimeOffset CreatedAtUtc { get; }

    public static VerificationRequestCreationResult Create(
        Guid id,
        string? clientReference,
        string? employeeReference,
        string? employerReference,
        IEnumerable<string?>? requestedData,
        DateTimeOffset createdAtUtc)
    {
        var errors = new List<VerificationRequestValidationError>();
        var normalizedClientReference = ValidateReference("clientReference", clientReference, errors);
        var normalizedEmployeeReference = ValidateReference("employeeReference", employeeReference, errors);
        var normalizedEmployerReference = ValidateReference("employerReference", employerReference, errors);
        var normalizedRequestedData = ValidateRequestedData(requestedData, errors);

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add(new(
                "createdAtUtc",
                "utc_required",
                "The creation timestamp must use UTC."));
        }

        if (errors.Count > 0)
        {
            return VerificationRequestCreationResult.Failure(errors);
        }

        var microsecondTicks = createdAtUtc.Ticks - (createdAtUtc.Ticks % 10);
        var normalizedTimestamp = new DateTimeOffset(microsecondTicks, TimeSpan.Zero);
        var request = new VerificationRequest(
            id,
            normalizedClientReference!,
            normalizedEmployeeReference!,
            normalizedEmployerReference!,
            normalizedRequestedData,
            normalizedTimestamp);

        return VerificationRequestCreationResult.Success(request);
    }

    private static string? ValidateReference(
        string field,
        string? value,
        ICollection<VerificationRequestValidationError> errors)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrEmpty(normalized))
        {
            errors.Add(new(field, "required", "A reference is required."));
            return normalized;
        }

        if (normalized.Length > MaximumReferenceLength)
        {
            errors.Add(new(field, "length_out_of_range", "A reference must contain 1 through 100 characters."));
        }

        if (!normalized.All(IsReferenceCharacter))
        {
            errors.Add(new(field, "invalid_format", "A reference contains an unsupported character."));
        }

        return normalized;
    }

    private static IReadOnlyList<RequestedData> ValidateRequestedData(
        IEnumerable<string?>? values,
        ICollection<VerificationRequestValidationError> errors)
    {
        var suppliedValues = values?.ToArray() ?? [];

        if (suppliedValues.Length == 0)
        {
            errors.Add(new("requestedData", "at_least_one_required", "At least one requested-data value is required."));
            return Array.Empty<RequestedData>();
        }

        var seen = new HashSet<string?>(StringComparer.Ordinal);
        var accepted = new HashSet<RequestedData>();

        foreach (var value in suppliedValues)
        {
            if (!seen.Add(value))
            {
                errors.Add(new("requestedData", "duplicate_value", "A requested-data value is duplicated."));
            }

            if (value is null || !RequestedDataByName.TryGetValue(value, out var requestedData))
            {
                errors.Add(new("requestedData", "unsupported_value", "A requested-data value is unsupported."));
                continue;
            }

            accepted.Add(requestedData);
        }

        return new ReadOnlyCollection<RequestedData>(accepted.Order().ToArray());
    }

    private static bool IsReferenceCharacter(char value) =>
        value is >= 'A' and <= 'Z'
        or >= 'a' and <= 'z'
        or >= '0' and <= '9'
        or '.' or '_' or '-';
}
