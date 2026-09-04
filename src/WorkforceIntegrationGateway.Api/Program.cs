using WorkforceIntegrationGateway.Api.ErrorHandling;
using WorkforceIntegrationGateway.Api.Requests;
using WorkforceIntegrationGateway.Application.VerificationRequests;
using WorkforceIntegrationGateway.Infrastructure.VerificationRequests;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<UnexpectedExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IVerificationRequestRepository, InMemoryVerificationRequestRepository>();
builder.Services.AddSingleton<IVerificationRequestIdGenerator, RandomVerificationRequestIdGenerator>();
builder.Services.AddSingleton<VerificationRequestRequestReader>();
builder.Services.AddScoped<VerificationRequestService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(_ => { });
app.MapControllers();

app.Run();

public partial class Program;
