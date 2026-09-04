using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkforceIntegrationGateway.Application.VerificationRequests;
using WorkforceIntegrationGateway.Api.Contracts;
using WorkforceIntegrationGateway.Domain.VerificationRequests;

namespace WorkforceIntegrationGateway.IntegrationTests.VerificationRequests;

public sealed class VerificationRequestApiTests
{
    private const string CollectionPath = "/api/v1/verification-requests";

    [Fact]
    public async Task Valid_post_returns_created_resource_and_get_returns_same_representation()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var post = await PostJsonAsync(client, ValidJson(), TestContext.Current.CancellationToken);
        var created = await ReadJsonAsync(post);

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.Equal("pending", created.GetProperty("status").GetString());
        Assert.Equal("client.synthetic-001", created.GetProperty("clientReference").GetString());
        Assert.Equal("employee.synthetic-001", created.GetProperty("employeeReference").GetString());
        Assert.Equal("employer.synthetic-001", created.GetProperty("employerReference").GetString());
        Assert.Equal(
            ["employmentStatus", "jobTitle", "employmentDates"],
            created.GetProperty("requestedData").EnumerateArray().Select(item => item.GetString()));
        Assert.Matches(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            created.GetProperty("id").GetString()!);
        Assert.Matches(
            "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\\.[0-9]{6}Z$",
            created.GetProperty("createdAtUtc").GetString()!);

        var expectedLocation = $"{CollectionPath}/{created.GetProperty("id").GetString()}";
        Assert.Equal(expectedLocation, post.Headers.Location?.OriginalString);

        using var get = await client.GetAsync(expectedLocation, TestContext.Current.CancellationToken);
        var retrieved = await ReadJsonAsync(get);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(created.GetRawText(), retrieved.GetRawText());
    }

    [Fact]
    public async Task Surrounding_reference_whitespace_is_returned_trimmed()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var json = ValidJson().Replace("client.synthetic-001", "  client.synthetic-001  ", StringComparison.Ordinal);

        using var response = await PostJsonAsync(client, json, TestContext.Current.CancellationToken);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("client.synthetic-001", body.GetProperty("clientReference").GetString());
    }

    [Fact]
    public async Task Uppercase_well_formed_identifier_is_accepted_and_response_is_canonical()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        using var post = await PostJsonAsync(client, ValidJson(), TestContext.Current.CancellationToken);
        var created = await ReadJsonAsync(post);
        var id = created.GetProperty("id").GetString()!;

        using var get = await client.GetAsync($"{CollectionPath}/{id.ToUpperInvariant()}", TestContext.Current.CancellationToken);
        var retrieved = await ReadJsonAsync(get);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(id, retrieved.GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"clientReference\":\"synthetic\",}")]
    public async Task Empty_or_malformed_json_returns_malformed_json_problem(string json)
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostJsonAsync(client, json, TestContext.Current.CancellationToken);

        await AssertValidationProblemAsync(response, "body", "malformed_json");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    public async Task Non_object_json_returns_invalid_shape_problem(string json)
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostJsonAsync(client, json, TestContext.Current.CancellationToken);

        await AssertValidationProblemAsync(response, "body", "invalid_json_shape");
    }

    [Fact]
    public async Task Null_and_missing_properties_return_stable_field_codes()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        const string json = """
            {"clientReference":null,"requestedData":null}
            """;

        using var response = await PostJsonAsync(client, json, TestContext.Current.CancellationToken);
        var errors = await ReadErrorsAsync(response);

        Assert.Contains(errors, error => error == "clientReference:required");
        Assert.Contains(errors, error => error == "employeeReference:required");
        Assert.Contains(errors, error => error == "employerReference:required");
        Assert.Contains(errors, error => error == "requestedData:at_least_one_required");
    }

    [Fact]
    public async Task Unknown_and_duplicate_properties_are_rejected()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        const string json = """
            {"clientReference":"first","clientReference":"second","employeeReference":"employee.synthetic-001","employerReference":"employer.synthetic-001","requestedData":["EmploymentStatus"],"<unknown>":true}
            """;

        using var response = await PostJsonAsync(client, json, TestContext.Current.CancellationToken);
        var errors = await ReadErrorsAsync(response);

        Assert.Contains("clientReference:duplicate_property", errors);
        Assert.Contains("body:unknown_property", errors);
        var rawBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("<unknown>", rawBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unsafe_duplicate_property_name_is_not_reflected_in_problem()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        const string json = """
            {"clientReference":"client.synthetic-001","employeeReference":"employee.synthetic-001","employerReference":"employer.synthetic-001","requestedData":["EmploymentStatus"],"<unsafe>":true,"<unsafe>":false}
            """;

        using var response = await PostJsonAsync(client, json, TestContext.Current.CancellationToken);
        var errors = await ReadErrorsAsync(response);
        var rawBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("body:unknown_property", errors);
        Assert.Contains("body:duplicate_property", errors);
        Assert.DoesNotContain("<unsafe>", rawBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"EmploymentStatus\"")]
    [InlineData("42")]
    [InlineData("{}")]
    public async Task Non_array_requested_data_is_rejected(string requestedData)
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var json = ValidJson().Replace(
            "[\"EmploymentDates\",\"EmploymentStatus\",\"JobTitle\"]",
            requestedData,
            StringComparison.Ordinal);

        using var response = await PostJsonAsync(client, json, TestContext.Current.CancellationToken);

        await AssertValidationProblemAsync(response, "requestedData", "invalid_json_shape");
    }

    [Fact]
    public async Task Non_string_requested_data_member_is_rejected()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var json = ValidJson().Replace("\"JobTitle\"", "42", StringComparison.Ordinal);

        using var response = await PostJsonAsync(client, json, TestContext.Current.CancellationToken);

        await AssertValidationProblemAsync(response, "requestedData", "invalid_json_shape");
    }

    [Fact]
    public async Task Trailing_json_content_is_rejected()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostJsonAsync(client, ValidJson() + " {}", TestContext.Current.CancellationToken);

        await AssertValidationProblemAsync(response, "body", "trailing_content");
    }

    [Fact]
    public async Task Semantic_validation_returns_all_safe_field_codes()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        const string json = """
            {"clientReference":" ","employeeReference":"invalid value","employerReference":"é","requestedData":["JobTitle","JobTitle","unknown"]}
            """;

        using var response = await PostJsonAsync(client, json, TestContext.Current.CancellationToken);
        var errors = await ReadErrorsAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            [
                "clientReference:required",
                "employeeReference:invalid_format",
                "employerReference:invalid_format",
                "requestedData:duplicate_value",
                "requestedData:unsupported_value"
            ],
            errors);
    }

    [Fact]
    public async Task Invalid_post_does_not_reach_storage()
    {
        await using var factory = new TestApiFactory(services =>
        {
            services.RemoveAll<IVerificationRequestRepository>();
            services.AddSingleton<IVerificationRequestRepository, ThrowingRepository>();
        });
        using var client = factory.CreateClient();

        using var response = await PostJsonAsync(client, "{}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Oversized_body_is_rejected_before_parsing()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var json = "{\"clientReference\":\"" + new string('a', 17_000) + "\"}";

        using var response = await PostJsonAsync(client, json, TestContext.Current.CancellationToken);

        await AssertValidationProblemAsync(response, "body", "invalid_json_shape");
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("11111111222233334444555555555555")]
    [InlineData("{11111111-2222-3333-4444-555555555555}")]
    public async Task Malformed_identifier_returns_validation_problem(string id)
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{CollectionPath}/{id}", TestContext.Current.CancellationToken);

        await AssertValidationProblemAsync(response, "id", "invalid_format");
    }

    [Fact]
    public async Task Unknown_well_formed_identifier_returns_not_found_problem()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"{CollectionPath}/00000000-0000-0000-0000-000000000000",
            TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "urn:wig:problem:verification-request-not-found",
            "verification_request_not_found");
    }

    [Fact]
    public async Task Unsupported_or_absent_content_type_returns_unsupported_media_type_problem()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        using var textRequest = new StringContent(ValidJson(), Encoding.UTF8, "text/plain");
        using var absentRequest = new ByteArrayContent(Encoding.UTF8.GetBytes(ValidJson()));

        using var unsupported = await client.PostAsync(CollectionPath, textRequest, TestContext.Current.CancellationToken);
        using var absent = await client.PostAsync(CollectionPath, absentRequest, TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            unsupported,
            HttpStatusCode.UnsupportedMediaType,
            "urn:wig:problem:unsupported-media-type",
            "unsupported_media_type");
        await AssertProblemAsync(
            absent,
            HttpStatusCode.UnsupportedMediaType,
            "urn:wig:problem:unsupported-media-type",
            "unsupported_media_type");
    }

    [Fact]
    public async Task Restarting_transient_host_loses_created_resource_as_documented()
    {
        string id;

        await using (var firstFactory = new TestApiFactory())
        using (var firstClient = firstFactory.CreateClient())
        using (var post = await PostJsonAsync(firstClient, ValidJson(), TestContext.Current.CancellationToken))
        {
            id = (await ReadJsonAsync(post)).GetProperty("id").GetString()!;
        }

        await using var secondFactory = new TestApiFactory();
        using var secondClient = secondFactory.CreateClient();
        using var response = await secondClient.GetAsync($"{CollectionPath}/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unexpected_repository_failure_returns_safe_unexpected_problem()
    {
        using var logs = new RecordingLoggerProvider();
        await using var factory = new TestApiFactory(services =>
        {
            services.RemoveAll<IVerificationRequestRepository>();
            services.AddSingleton<IVerificationRequestRepository, ThrowingRepository>();
        }, logs);
        using var client = factory.CreateClient();

        using var response = await PostJsonAsync(client, ValidJson(), TestContext.Current.CancellationToken);
        var rawBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.InternalServerError,
            "urn:wig:problem:unexpected",
            "unexpected_error");
        Assert.DoesNotContain(nameof(ThrowingRepository), rawBody, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ", rawBody, StringComparison.Ordinal);
        var errorLogs = logs.Entries.Where(entry => entry.Level >= LogLevel.Error).ToArray();
        Assert.NotEmpty(errorLogs);
        Assert.All(errorLogs, entry => Assert.Null(entry.Exception));
        Assert.DoesNotContain(errorLogs, entry => entry.Message.Contains(nameof(ThrowingRepository), StringComparison.Ordinal));
        Assert.DoesNotContain(errorLogs, entry => entry.Message.Contains("synthetic", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(errorLogs, entry => entry.Message.Contains(":\\", StringComparison.Ordinal));
        Assert.DoesNotContain(errorLogs, entry => entry.Message.Contains(" at ", StringComparison.Ordinal));
    }

    [Fact]
    public void Response_timestamp_is_invariant_under_non_gregorian_current_culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var creation = VerificationRequest.Create(
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                "client.synthetic-001",
                "employee.synthetic-001",
                "employer.synthetic-001",
                ["EmploymentStatus"],
                new DateTimeOffset(2026, 9, 4, 12, 34, 56, 789, TimeSpan.Zero).AddTicks(1_230));

            var response = VerificationRequestResponse.FromDomain(creation.Request!);

            Assert.Equal("2026-09-04T12:34:56.789123Z", response.CreatedAtUtc);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task Concurrent_posts_create_distinct_retrievable_resources()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var posts = Enumerable.Range(0, 20)
            .Select(index => PostJsonAsync(
                client,
                ValidJson().Replace("client.synthetic-001", $"client.synthetic-{index:D3}", StringComparison.Ordinal),
                TestContext.Current.CancellationToken))
            .ToArray();

        var responses = await Task.WhenAll(posts);

        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
            var bodies = await Task.WhenAll(responses.Select(ReadJsonAsync));
            var ids = bodies.Select(body => body.GetProperty("id").GetString()).ToArray();
            Assert.Equal(20, ids.Distinct().Count());
            var retrievals = await Task.WhenAll(ids.Select(id =>
                client.GetAsync($"{CollectionPath}/{id}", TestContext.Current.CancellationToken)));

            try
            {
                Assert.All(retrievals, retrieval => Assert.Equal(HttpStatusCode.OK, retrieval.StatusCode));
            }
            finally
            {
                foreach (var retrieval in retrievals)
                {
                    retrieval.Dispose();
                }
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Development_openapi_contains_the_versioned_collection_route()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(CollectionPath, body, StringComparison.Ordinal);
    }

    [Fact]
    public void Checked_in_openapi_records_the_accepted_contract_surface()
    {
        var contractPath = Path.Combine(FindRepositoryRoot(), "documentation", "openapi.json");
        using var contract = JsonDocument.Parse(File.ReadAllText(contractPath));
        var root = contract.RootElement;
        var paths = root.GetProperty("paths");
        var collection = paths.GetProperty(CollectionPath);
        var resource = paths.GetProperty($"{CollectionPath}/{{id}}");
        var schemas = root.GetProperty("components").GetProperty("schemas");

        Assert.Equal("3.1.0", root.GetProperty("openapi").GetString());
        Assert.True(collection.TryGetProperty("post", out var post));
        Assert.True(resource.TryGetProperty("get", out _));
        Assert.Equal(
            "#/components/schemas/CreateVerificationRequest",
            post.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString());
        Assert.Equal(
            "^/api/v1/verification-requests/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            post.GetProperty("responses").GetProperty("201").GetProperty("headers").GetProperty("Location")
                .GetProperty("schema").GetProperty("pattern").GetString());
        Assert.False(schemas.GetProperty("CreateVerificationRequest").GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            "^\\s*[A-Za-z0-9._-]{1,100}\\s*$",
            schemas.GetProperty("SyntheticReference").GetProperty("pattern").GetString());
        Assert.Equal(
            ["EmploymentStatus", "JobTitle", "EmploymentDates"],
            schemas.GetProperty("CreateVerificationRequest").GetProperty("properties").GetProperty("requestedData")
                .GetProperty("items").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            schemas.GetProperty("VerificationRequest").GetProperty("properties").GetProperty("id")
                .GetProperty("pattern").GetString());
        Assert.Equal(
            "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\\.[0-9]{6}Z$",
            schemas.GetProperty("VerificationRequest").GetProperty("properties").GetProperty("createdAtUtc")
                .GetProperty("pattern").GetString());
        Assert.Equal(
            "pending",
            schemas.GetProperty("VerificationRequest").GetProperty("properties").GetProperty("status")
                .GetProperty("const").GetString());
        Assert.Equal(
            "urn:wig:problem:validation",
            schemas.GetProperty("ValidationProblem").GetProperty("allOf")[1].GetProperty("properties")
                .GetProperty("type").GetProperty("const").GetString());
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client,
        string json,
        CancellationToken cancellationToken)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync(CollectionPath, content, cancellationToken);
    }

    private static string ValidJson() =>
        """
        {"clientReference":"client.synthetic-001","employeeReference":"employee.synthetic-001","employerReference":"employer.synthetic-001","requestedData":["EmploymentDates","EmploymentStatus","JobTitle"]}
        """;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WorkforceIntegrationGateway.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the Workforce Integration Gateway repository root.");
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private static async Task<IReadOnlyList<string>> ReadErrorsAsync(HttpResponseMessage response)
    {
        var body = await ReadJsonAsync(response);
        return body.GetProperty("errors")
            .EnumerateArray()
            .Select(error => $"{error.GetProperty("field").GetString()}:{error.GetProperty("code").GetString()}")
            .ToArray();
    }

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response,
        string field,
        string code)
    {
        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "urn:wig:problem:validation",
            "validation_failed");
        Assert.Contains($"{field}:{code}", await ReadErrorsAsync(response));
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string type,
        string code)
    {
        var body = await ReadJsonAsync(response);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(type, body.GetProperty("type").GetString());
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
    }

    private sealed class TestApiFactory(
        Action<IServiceCollection>? configureServices = null,
        ILoggerProvider? loggerProvider = null)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            if (configureServices is not null)
            {
                builder.ConfigureServices(configureServices);
            }

            if (loggerProvider is not null)
            {
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(loggerProvider);
                });
            }
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<CapturedLog> entries = new();

        public IReadOnlyCollection<CapturedLog> Entries => entries.ToArray();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(entries);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(ConcurrentQueue<CapturedLog> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(new(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record CapturedLog(LogLevel Level, string Message, Exception? Exception);

    private sealed class ThrowingRepository : IVerificationRequestRepository
    {
        public ValueTask AddAsync(VerificationRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated internal repository failure.");

        public ValueTask<VerificationRequest?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated internal repository failure.");
    }
}
