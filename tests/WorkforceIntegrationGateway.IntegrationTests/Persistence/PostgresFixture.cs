using Microsoft.EntityFrameworkCore;
using Npgsql;
using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;
using WorkforceIntegrationGateway.Infrastructure.Persistence;

namespace WorkforceIntegrationGateway.IntegrationTests.Persistence;

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Image = "postgres:18.6-alpine3.24@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder(Image)
        .WithDatabase("wig_fixture")
        .WithUsername("wig_fixture")
        .WithPassword("synthetic-fixture-password")
        .WithName($"wig-tests-{Guid.NewGuid():N}")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted(
            "pg_isready --host localhost --dbname wig_fixture --username wig_fixture",
            wait => wait.WithInterval(TimeSpan.FromSeconds(1)).WithTimeout(TimeSpan.FromSeconds(60))))
        .Build();

    public string ContainerName => container.Name;

    public async ValueTask InitializeAsync()
    {
        try
        {
            await container.StartAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Docker with Linux containers is required for PostgreSQL integration tests. Verify Docker Desktop is running.",
                exception);
        }
    }

    public async Task<string> CreateDatabaseAsync(bool migrate = true)
    {
        var databaseName = $"wig_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE {databaseName}";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var builder = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = databaseName,
            Timeout = 3,
            CommandTimeout = 5
        };

        if (migrate)
        {
            await using var context = CreateContext(builder.ConnectionString);
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        return builder.ConnectionString;
    }

    public GatewayDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new GatewayDbContext(options);
    }

    public Task StopAsync() => container.StopAsync();

    public Task StartAsync() => container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}

[CollectionDefinition("PostgreSQL", DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
