using WorkforceIntegrationGateway.Api.ErrorHandling;
using WorkforceIntegrationGateway.Api.Requests;
using WorkforceIntegrationGateway.Application.VerificationRequests;
using WorkforceIntegrationGateway.Infrastructure.Persistence;
using WorkforceIntegrationGateway.Infrastructure.VerificationRequests;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<UnexpectedExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddWorkforceGatewayPersistence(builder.Configuration);
builder.Services.AddSingleton<IVerificationRequestIdGenerator, RandomVerificationRequestIdGenerator>();
builder.Services.AddSingleton<VerificationRequestRequestReader>();
builder.Services.AddScoped<VerificationRequestService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    using var startupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));

    try
    {
        await scope.ServiceProvider.GetRequiredService<IDatabaseStartupValidator>()
            .ValidateAsync(startupDeadline.Token);
    }
    catch (OperationCanceledException) when (startupDeadline.IsCancellationRequested)
    {
        throw new DatabaseStartupException(
            "The WorkforceGateway database validation timed out. Verify PostgreSQL health and network reachability.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(_ => { });
app.MapControllers();

app.Run();

public partial class Program;
