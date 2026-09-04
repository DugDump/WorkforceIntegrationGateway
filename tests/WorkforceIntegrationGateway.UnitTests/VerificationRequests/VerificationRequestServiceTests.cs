using WorkforceIntegrationGateway.Application.VerificationRequests;
using WorkforceIntegrationGateway.Domain.VerificationRequests;

namespace WorkforceIntegrationGateway.UnitTests.VerificationRequests;

public sealed class VerificationRequestServiceTests
{
    private static readonly Guid Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly DateTimeOffset Timestamp =
        new DateTimeOffset(2026, 9, 4, 15, 45, 30, TimeSpan.Zero).AddTicks(9);

    [Fact]
    public async Task Create_assigns_controlled_identity_and_time_then_persists_once()
    {
        var repository = new RecordingRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(ValidCommand(), TestContext.Current.CancellationToken);

        var request = Assert.IsType<VerificationRequest>(result.Request);
        Assert.Equal(Id, request.Id);
        Assert.Equal(Timestamp.AddTicks(-9), request.CreatedAtUtc);
        Assert.Same(request, Assert.Single(repository.Added));
    }

    [Fact]
    public async Task Invalid_creation_does_not_call_repository()
    {
        var repository = new RecordingRepository();
        var service = CreateService(repository);
        var command = ValidCommand() with { ClientReference = null };

        var result = await service.CreateAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Find_returns_repository_result()
    {
        var repository = new RecordingRepository();
        var service = CreateService(repository);
        var created = await service.CreateAsync(ValidCommand(), TestContext.Current.CancellationToken);

        var found = await service.FindAsync(Id, TestContext.Current.CancellationToken);

        Assert.Same(created.Request, found);
    }

    private static VerificationRequestService CreateService(RecordingRepository repository) =>
        new(repository, new FixedIdGenerator(), new FixedTimeProvider());

    private static CreateVerificationRequestCommand ValidCommand() => new(
        "client.synthetic-001",
        "employee.synthetic-001",
        "employer.synthetic-001",
        ["EmploymentStatus"]);

    private sealed class FixedIdGenerator : IVerificationRequestIdGenerator
    {
        public Guid Create() => Id;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Timestamp;
    }

    private sealed class RecordingRepository : IVerificationRequestRepository
    {
        internal List<VerificationRequest> Added { get; } = [];

        public ValueTask AddAsync(VerificationRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Added.Add(request);
            return ValueTask.CompletedTask;
        }

        public ValueTask<VerificationRequest?> FindAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Added.SingleOrDefault(request => request.Id == id));
        }
    }
}
