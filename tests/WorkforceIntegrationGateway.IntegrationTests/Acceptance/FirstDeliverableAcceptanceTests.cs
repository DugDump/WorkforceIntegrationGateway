using System.Net;
using System.Text;
using System.Text.Json;
using Npgsql;
using WorkforceIntegrationGateway.IntegrationTests.Persistence;

namespace WorkforceIntegrationGateway.IntegrationTests.Acceptance;

[Collection("PostgreSQL")]
public sealed class FirstDeliverableAcceptanceTests(PostgresFixture fixture)
{
    private const string CollectionPath = "/api/v1/verification-requests";

    [Fact]
    public async Task Create_retrieve_and_rehost_preserve_the_accepted_representation()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        string location;
        string createdBody;

        await using (var firstHost = new PostgresApiFactory(connectionString))
        using (var firstClient = firstHost.CreateClient())
        using (var created = await PostAsync(firstClient, ValidJson()))
        {
            createdBody = await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(createdBody);
            var root = document.RootElement;

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal("pending", root.GetProperty("status").GetString());
            Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", root.GetProperty("id").GetString()!);
            Assert.Matches("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\\.[0-9]{6}Z$", root.GetProperty("createdAtUtc").GetString()!);
            Assert.Equal(
                ["employmentStatus", "jobTitle", "employmentDates"],
                root.GetProperty("requestedData").EnumerateArray().Select(item => item.GetString()));
            location = created.Headers.Location!.OriginalString;
            Assert.Equal($"{CollectionPath}/{root.GetProperty("id").GetString()}", location);

            using var retrieved = await firstClient.GetAsync(location, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, retrieved.StatusCode);
            Assert.Equal(createdBody, await retrieved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        await using var secondHost = new PostgresApiFactory(connectionString);
        using var secondClient = secondHost.CreateClient();
        using var afterRestart = await secondClient.GetAsync(location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, afterRestart.StatusCode);
        Assert.Equal(createdBody, await afterRestart.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Representative_domain_and_malformed_failures_return_400_and_persist_nothing()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var host = new PostgresApiFactory(connectionString);
        using var client = host.CreateClient();

        using var invalid = await PostAsync(client, ValidJson().Replace("client.synthetic-acceptance", "bad value", StringComparison.Ordinal));
        using var malformed = await PostAsync(client, "{");

        await AssertProblemAsync(invalid, HttpStatusCode.BadRequest, "urn:wig:problem:validation", "validation_failed");
        await AssertProblemAsync(malformed, HttpStatusCode.BadRequest, "urn:wig:problem:validation", "validation_failed");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var count = new NpgsqlCommand("SELECT COUNT(*) FROM gateway.verification_requests", connection);
        Assert.Equal(0L, await count.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Malformed_and_unknown_identifiers_return_distinct_safe_problems()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var host = new PostgresApiFactory(connectionString);
        using var client = host.CreateClient();

        using var malformed = await client.GetAsync($"{CollectionPath}/not-a-uuid", TestContext.Current.CancellationToken);
        using var unknown = await client.GetAsync($"{CollectionPath}/00000000-0000-0000-0000-000000000000", TestContext.Current.CancellationToken);

        await AssertProblemAsync(malformed, HttpStatusCode.BadRequest, "urn:wig:problem:validation", "validation_failed");
        await AssertProblemAsync(unknown, HttpStatusCode.NotFound, "urn:wig:problem:verification-request-not-found", "verification_request_not_found");
    }

    [Fact]
    public async Task Runtime_database_outage_returns_safe_503()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var host = new PostgresApiFactory(connectionString);
        using var client = host.CreateClient();
        using (var healthy = await client.GetAsync($"{CollectionPath}/00000000-0000-0000-0000-000000000000", TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, healthy.StatusCode);
        }

        await fixture.StopAsync();

        try
        {
            using var response = await PostAsync(client, ValidJson());
            var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "urn:wig:problem:persistence-unavailable", "persistence_unavailable");
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

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string json) =>
        client.PostAsync(
            CollectionPath,
            new StringContent(json, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

    private static string ValidJson() =>
        """
        {"clientReference":"client.synthetic-acceptance","employeeReference":"employee.synthetic-acceptance","employerReference":"employer.synthetic-acceptance","requestedData":["EmploymentDates","EmploymentStatus","JobTitle"]}
        """;

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string type,
        string code)
    {
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(raw);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(type, document.RootElement.GetProperty("type").GetString());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("ConnectionString", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":\\", raw, StringComparison.Ordinal);
    }
}
