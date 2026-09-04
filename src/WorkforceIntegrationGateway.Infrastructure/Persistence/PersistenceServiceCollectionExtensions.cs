using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WorkforceIntegrationGateway.Application.VerificationRequests;

namespace WorkforceIntegrationGateway.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddWorkforceGatewayPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WorkforceGateway");
        ValidateConnectionString(connectionString);

        services.AddDbContext<GatewayDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IVerificationRequestRepository, PostgresVerificationRequestRepository>();
        services.AddScoped<IDatabaseStartupValidator, DatabaseStartupValidator>();
        return services;
    }

    private static void ValidateConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DatabaseStartupException(
                "ConnectionStrings:WorkforceGateway is required. Configure it with user-secrets or ConnectionStrings__WorkforceGateway.");
        }

        try
        {
            var parsed = new NpgsqlConnectionStringBuilder(connectionString);

            if (string.IsNullOrWhiteSpace(parsed.Host)
                || string.IsNullOrWhiteSpace(parsed.Database)
                || string.IsNullOrWhiteSpace(parsed.Username))
            {
                throw new DatabaseStartupException(
                    "ConnectionStrings:WorkforceGateway must include Host, Database, and Username.");
            }
        }
        catch (ArgumentException)
        {
            throw new DatabaseStartupException(
                "ConnectionStrings:WorkforceGateway is malformed. Configure a valid PostgreSQL connection string.");
        }
    }
}
