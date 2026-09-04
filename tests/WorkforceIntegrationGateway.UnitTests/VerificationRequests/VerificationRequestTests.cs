using WorkforceIntegrationGateway.Domain.VerificationRequests;

namespace WorkforceIntegrationGateway.UnitTests.VerificationRequests;

public sealed class VerificationRequestTests
{
    private static readonly Guid Id = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 4, 12, 30, 45, TimeSpan.Zero);

    [Fact]
    public void Create_returns_the_canonical_pending_request()
    {
        var result = Create(
            clientReference: " client.synthetic-001 ",
            employeeReference: " employee_synthetic-001 ",
            employerReference: " employer.synthetic-001 ",
            requestedData: ["EmploymentDates", "EmploymentStatus", "JobTitle"]);

        Assert.True(result.IsSuccess);
        var request = Assert.IsType<VerificationRequest>(result.Request);
        Assert.Empty(result.Errors);
        Assert.Equal(Id, request.Id);
        Assert.Equal("client.synthetic-001", request.ClientReference);
        Assert.Equal("employee_synthetic-001", request.EmployeeReference);
        Assert.Equal("employer.synthetic-001", request.EmployerReference);
        Assert.Equal(VerificationRequestStatus.Pending, request.Status);
        Assert.Equal(CreatedAtUtc, request.CreatedAtUtc);
        Assert.Equal(
            [RequestedData.EmploymentStatus, RequestedData.JobTitle, RequestedData.EmploymentDates],
            request.RequestedData);
    }

    [Theory]
    [InlineData("clientReference")]
    [InlineData("employeeReference")]
    [InlineData("employerReference")]
    public void Create_accepts_reference_lengths_from_one_through_one_hundred(string field)
    {
        Assert.True(CreateWithReference(field, "a").IsSuccess);
        Assert.True(CreateWithReference(field, new string('a', 100)).IsSuccess);
    }

    [Theory]
    [InlineData("clientReference")]
    [InlineData("employeeReference")]
    [InlineData("employerReference")]
    public void Create_rejects_null_empty_or_whitespace_reference(string field)
    {
        AssertError(CreateWithReference(field, null), field, "required");
        AssertError(CreateWithReference(field, string.Empty), field, "required");
        AssertError(CreateWithReference(field, "   "), field, "required");
    }

    [Theory]
    [InlineData("clientReference")]
    [InlineData("employeeReference")]
    [InlineData("employerReference")]
    public void Create_rejects_reference_over_one_hundred_characters(string field)
    {
        AssertError(CreateWithReference(field, new string('a', 101)), field, "length_out_of_range");
    }

    [Theory]
    [InlineData("clientReference")]
    [InlineData("employeeReference")]
    [InlineData("employerReference")]
    public void Create_rejects_non_ascii_or_unapproved_reference_characters(string field)
    {
        AssertError(CreateWithReference(field, "synthetic value"), field, "invalid_format");
        AssertError(CreateWithReference(field, "synthetic-é"), field, "invalid_format");
    }

    [Fact]
    public void Create_rejects_no_requested_data()
    {
        AssertError(Create(requestedData: []), "requestedData", "at_least_one_required");

        var nullResult = VerificationRequest.Create(
            Id,
            "client.synthetic-001",
            "employee.synthetic-001",
            "employer.synthetic-001",
            null,
            CreatedAtUtc);

        AssertError(nullResult, "requestedData", "at_least_one_required");
    }

    [Fact]
    public void Create_rejects_duplicate_requested_data()
    {
        AssertError(
            Create(requestedData: ["JobTitle", "JobTitle"]),
            "requestedData",
            "duplicate_value");
    }

    [Theory]
    [InlineData("jobTitle")]
    [InlineData("Unknown")]
    [InlineData("0")]
    [InlineData(null)]
    public void Create_rejects_unknown_or_non_symbolic_requested_data(string? value)
    {
        AssertError(Create(requestedData: [value]), "requestedData", "unsupported_value");
    }

    [Fact]
    public void Create_returns_all_independently_detectable_errors_in_stable_order()
    {
        var result = VerificationRequest.Create(
            Id,
            " ",
            new string('a', 101) + "!",
            "invalid value",
            ["Unknown", "Unknown"],
            CreatedAtUtc.ToOffset(TimeSpan.FromHours(-4)));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Request);
        Assert.Equal(
            [
                "clientReference:required",
                "employeeReference:length_out_of_range",
                "employeeReference:invalid_format",
                "employerReference:invalid_format",
                "requestedData:unsupported_value",
                "requestedData:duplicate_value",
                "requestedData:unsupported_value",
                "createdAtUtc:utc_required"
            ],
            result.Errors.Select(error => $"{error.Field}:{error.Code}"));
    }

    [Fact]
    public void Create_normalizes_timestamp_to_microsecond_precision()
    {
        var timestamp = new DateTimeOffset(CreatedAtUtc.Ticks + 9, TimeSpan.Zero);

        var request = Assert.IsType<VerificationRequest>(Create(createdAtUtc: timestamp).Request);

        Assert.Equal(CreatedAtUtc, request.CreatedAtUtc);
    }

    [Fact]
    public void Requested_data_observation_cannot_mutate_internal_state()
    {
        var request = Assert.IsType<VerificationRequest>(Create().Request);
        var mutableView = Assert.IsAssignableFrom<IList<RequestedData>>(request.RequestedData);

        Assert.Throws<NotSupportedException>(() => mutableView.Add(RequestedData.JobTitle));
        Assert.Equal([RequestedData.EmploymentStatus], request.RequestedData);
    }

    private static VerificationRequestCreationResult Create(
        string? clientReference = "client.synthetic-001",
        string? employeeReference = "employee.synthetic-001",
        string? employerReference = "employer.synthetic-001",
        IEnumerable<string?>? requestedData = null,
        DateTimeOffset? createdAtUtc = null) =>
        VerificationRequest.Create(
            Id,
            clientReference,
            employeeReference,
            employerReference,
            requestedData ?? ["EmploymentStatus"],
            createdAtUtc ?? CreatedAtUtc);

    private static VerificationRequestCreationResult CreateWithReference(string field, string? value) =>
        field switch
        {
            "clientReference" => Create(clientReference: value),
            "employeeReference" => Create(employeeReference: value),
            "employerReference" => Create(employerReference: value),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };

    private static void AssertError(
        VerificationRequestCreationResult result,
        string field,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Request);
        Assert.Contains(result.Errors, error => error.Field == field && error.Code == code);
    }
}
