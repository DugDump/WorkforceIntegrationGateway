using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkforceIntegrationGateway.Infrastructure.Persistence;

public sealed class GatewayDbContextFactory : IDesignTimeDbContextFactory<GatewayDbContext>
{
    public GatewayDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__WorkforceGateway")
            ?? throw new DatabaseStartupException(
                "ConnectionStrings__WorkforceGateway is required when running dotnet-ef. Set it to the target PostgreSQL database before applying migrations.");
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new GatewayDbContext(options);
    }
}
