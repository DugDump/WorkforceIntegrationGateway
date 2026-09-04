using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WorkforceIntegrationGateway.Application.VerificationRequests;
using WorkforceIntegrationGateway.Domain.VerificationRequests;
using WorkforceIntegrationGateway.Infrastructure.Persistence;

namespace WorkforceIntegrationGateway.IntegrationTests.Persistence;

[Collection("PostgreSQL")]
public sealed class PostgresPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Empty_database_migrates_to_complete_current_schema()
    {
        var connectionString = await fixture.CreateDatabaseAsync(migrate: false);
        await using var context = fixture.CreateContext(connectionString);

        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var pending = await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'gateway' AND table_name IN ('verification_requests', 'verification_request_data')",
            connection);

        Assert.Empty(pending);
        Assert.Equal(2L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Repository_round_trip_preserves_canonical_domain_representation()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var createdAt = new DateTimeOffset(2026, 9, 4, 12, 34, 56, 789, TimeSpan.Zero).AddTicks(1_230);
        var request = VerificationRequest.Create(
            id,
            "client.synthetic-001",
            "employee.synthetic-001",
            "employer.synthetic-001",
            ["EmploymentDates", "EmploymentStatus", "JobTitle"],
            createdAt).Request!;

        await using (var writeContext = fixture.CreateContext(connectionString))
        {
            var repository = new PostgresVerificationRequestRepository(writeContext);
            await repository.AddAsync(request, TestContext.Current.CancellationToken);
        }

        await using var readContext = fixture.CreateContext(connectionString);
        var retrieved = await new PostgresVerificationRequestRepository(readContext)
            .FindAsync(id, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal(request.Id, retrieved.Id);
        Assert.Equal(request.ClientReference, retrieved.ClientReference);
        Assert.Equal(request.EmployeeReference, retrieved.EmployeeReference);
        Assert.Equal(request.EmployerReference, retrieved.EmployerReference);
        Assert.Equal(request.RequestedData, retrieved.RequestedData);
        Assert.Equal(request.CreatedAtUtc, retrieved.CreatedAtUtc);
    }

    [Fact]
    public async Task Database_constraints_reject_invalid_rows_and_duplicates()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await AssertSqlStateAsync(connection,
            "INSERT INTO gateway.verification_requests VALUES ('11111111-2222-3333-4444-555555555551', 'bad value', 'employee.synthetic', 'employer.synthetic', 'Pending', now())",
            PostgresErrorCodes.CheckViolation);
        await AssertSqlStateAsync(connection,
            "INSERT INTO gateway.verification_requests VALUES ('11111111-2222-3333-4444-555555555552', 'client.synthetic', 'employee.synthetic', 'employer.synthetic', 'Complete', now())",
            PostgresErrorCodes.CheckViolation);

        await ExecuteAsync(connection,
            "INSERT INTO gateway.verification_requests VALUES ('11111111-2222-3333-4444-555555555553', 'client.synthetic', 'employee.synthetic', 'employer.synthetic', 'Pending', now())");
        await ExecuteAsync(connection,
            "INSERT INTO gateway.verification_request_data VALUES ('11111111-2222-3333-4444-555555555553', 'EmploymentStatus', 0)");
        await AssertSqlStateAsync(connection,
            "INSERT INTO gateway.verification_request_data VALUES ('11111111-2222-3333-4444-555555555553', 'EmploymentStatus', 1)",
            PostgresErrorCodes.UniqueViolation);
        await AssertSqlStateAsync(connection,
            "INSERT INTO gateway.verification_request_data VALUES ('11111111-2222-3333-4444-555555555553', 'Salary', 1)",
            PostgresErrorCodes.CheckViolation);
        await AssertSqlStateAsync(connection,
            "INSERT INTO gateway.verification_request_data VALUES ('11111111-2222-3333-4444-555555555554', 'JobTitle', 0)",
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Child_write_failure_rolls_back_parent_and_is_not_classified_as_unavailable()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using (var setup = new NpgsqlConnection(connectionString))
        {
            await setup.OpenAsync(TestContext.Current.CancellationToken);
            await ExecuteAsync(setup, "CREATE FUNCTION gateway.reject_child() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'synthetic child rejection'; END; $$");
            await ExecuteAsync(setup, "CREATE TRIGGER reject_child BEFORE INSERT ON gateway.verification_request_data FOR EACH ROW EXECUTE FUNCTION gateway.reject_child()");
        }

        var request = VerificationRequest.Create(
            Guid.Parse("11111111-2222-3333-4444-555555555559"),
            "client.synthetic",
            "employee.synthetic",
            "employer.synthetic",
            ["EmploymentStatus"],
            DateTimeOffset.UtcNow).Request!;
        await using (var context = fixture.CreateContext(connectionString))
        {
            var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
                new PostgresVerificationRequestRepository(context).AddAsync(request, TestContext.Current.CancellationToken).AsTask());
            Assert.IsNotType<PersistenceUnavailableException>(exception);
        }

        await using var verification = new NpgsqlConnection(connectionString);
        await verification.OpenAsync(TestContext.Current.CancellationToken);
        await using var count = new NpgsqlCommand(
            "SELECT COUNT(*) FROM gateway.verification_requests WHERE id = '11111111-2222-3333-4444-555555555559'",
            verification);
        Assert.Equal(0L, await count.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Startup_validation_distinguishes_current_unapplied_and_unknown_schema()
    {
        var current = await fixture.CreateDatabaseAsync();
        await ValidateAsync(current);

        var unapplied = await fixture.CreateDatabaseAsync(migrate: false);
        var unappliedException = await Assert.ThrowsAsync<DatabaseStartupException>(() => ValidateAsync(unapplied));
        Assert.Contains("schema is not current", unappliedException.Message, StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(current);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(connection, "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('99999999999999_Unknown', '10.0.11')");
        var unknownException = await Assert.ThrowsAsync<DatabaseStartupException>(() => ValidateAsync(current));
        Assert.Contains("unknown", unknownException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_and_malformed_configuration_fail_without_echoing_values()
    {
        var missing = new ConfigurationBuilder().Build();
        Assert.Throws<DatabaseStartupException>(() =>
            new ServiceCollection().AddWorkforceGatewayPersistence(missing));

        const string secret = "do-not-echo-secret";
        var malformed = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:WorkforceGateway"] = $"bad={secret}" }).Build();
        var exception = Assert.Throws<DatabaseStartupException>(() =>
            new ServiceCollection().AddWorkforceGatewayPersistence(malformed));

        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ef_tooling_requires_explicit_target_database()
    {
        var original = Environment.GetEnvironmentVariable("ConnectionStrings__WorkforceGateway");

        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__WorkforceGateway", null);
            var exception = Assert.Throws<DatabaseStartupException>(() =>
                new GatewayDbContextFactory().CreateDbContext([]));
            Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__WorkforceGateway", original);
        }
    }

    [Fact]
    public async Task Pre_cancelled_write_remains_cancellation_and_persists_nothing()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var request = VerificationRequest.Create(
            Guid.NewGuid(), "client.synthetic", "employee.synthetic", "employer.synthetic",
            ["EmploymentStatus"], DateTimeOffset.UtcNow).Request!;

        await using (var context = fixture.CreateContext(connectionString))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new PostgresVerificationRequestRepository(context).AddAsync(request, cancellation.Token).AsTask());
        }

        await using var verification = new NpgsqlConnection(connectionString);
        await verification.OpenAsync(TestContext.Current.CancellationToken);
        await using var count = new NpgsqlCommand("SELECT COUNT(*) FROM gateway.verification_requests", verification);
        Assert.Equal(0L, await count.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Database_becoming_unavailable_after_startup_returns_safe_503()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var factory = new PostgresApiFactory(connectionString);
        using var client = factory.CreateClient();

        using (var healthy = await client.GetAsync(
            "/api/v1/verification-requests/00000000-0000-0000-0000-000000000000",
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, healthy.StatusCode);
        }

        await fixture.StopAsync();

        try
        {
            using var content = new StringContent(
                """
                {"clientReference":"client.synthetic-outage","employeeReference":"employee.synthetic-outage","employerReference":"employer.synthetic-outage","requestedData":["EmploymentStatus"]}
                """,
                Encoding.UTF8,
                "application/json");
            using var response = await client.PostAsync(
                "/api/v1/verification-requests",
                content,
                TestContext.Current.CancellationToken);
            var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using var problem = JsonDocument.Parse(raw);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("urn:wig:problem:persistence-unavailable", problem.RootElement.GetProperty("type").GetString());
            Assert.Equal("persistence_unavailable", problem.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain("Host=", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Npgsql", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" at ", raw, StringComparison.Ordinal);
        }
        finally
        {
            await fixture.StartAsync();
        }
    }

    [Fact]
    public async Task Accepted_resource_survives_full_api_host_recreation()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        string location;
        string createdBody;

        await using (var firstFactory = new PostgresApiFactory(connectionString))
        using (var firstClient = firstFactory.CreateClient())
        using (var content = new StringContent(
            """
            {"clientReference":"client.synthetic-restart","employeeReference":"employee.synthetic-restart","employerReference":"employer.synthetic-restart","requestedData":["EmploymentDates","EmploymentStatus","JobTitle"]}
            """,
            Encoding.UTF8,
            "application/json"))
        using (var created = await firstClient.PostAsync(
            "/api/v1/verification-requests",
            content,
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            location = created.Headers.Location!.OriginalString;
            createdBody = await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }

        await using var secondFactory = new PostgresApiFactory(connectionString);
        using var secondClient = secondFactory.CreateClient();
        using var retrieved = await secondClient.GetAsync(location, TestContext.Current.CancellationToken);
        var retrievedBody = await retrieved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, retrieved.StatusCode);
        Assert.Equal(createdBody, retrievedBody);
    }

    private async Task ValidateAsync(string connectionString)
    {
        await using var context = fixture.CreateContext(connectionString);
        await new DatabaseStartupValidator(context).ValidateAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task AssertSqlStateAsync(NpgsqlConnection connection, string sql, string state)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, sql));
        Assert.Equal(state, exception.SqlState);
    }

}
