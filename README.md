# Workforce Integration Gateway

Workforce Integration Gateway is a fictional ASP.NET Core reference service for
demonstrating production-oriented .NET backend engineering. It will accept
synthetic employment-verification requests and, in later increments, integrate
with simulated payroll and HR providers. It has no affiliation with an employer
or verification provider and must never contain real employee data or Social
Security numbers.

## Current status

The repository currently provides a versioned local demonstration API for
creating and retrieving synthetic verification requests in their initial
`Pending` state. Storage is intentionally in memory for this increment, so an
API restart loses created requests. PostgreSQL durability, provider integration,
authentication, asynchronous processing, deployment, and production readiness
are not implemented yet.

## Solution structure

Production dependencies point inward:

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application -> Domain
```

- `WorkforceIntegrationGateway.Api` hosts ASP.NET Core and composes the service.
- `WorkforceIntegrationGateway.Application` will own use cases and ports.
- `WorkforceIntegrationGateway.Domain` will own canonical business concepts.
- `WorkforceIntegrationGateway.Infrastructure` will implement external adapters.
- `WorkforceIntegrationGateway.UnitTests` is reserved for focused behavior tests.
- `WorkforceIntegrationGateway.IntegrationTests` is reserved for integration-boundary tests.
- `WorkforceIntegrationGateway.ArchitectureTests` enforces production project references.

The public repository is independently buildable. Private engineering guidance
and backlog material are intentionally excluded.

## Prerequisites

- .NET SDK 10.0.400 or a compatible later 10.0 feature band selected by `global.json`.

## Restore, build, and test

From the repository root:

```powershell
dotnet restore WorkforceIntegrationGateway.sln --locked-mode
dotnet build WorkforceIntegrationGateway.sln --no-restore
dotnet run --project tests/WorkforceIntegrationGateway.ArchitectureTests --no-build
dotnet run --project tests/WorkforceIntegrationGateway.UnitTests --no-build
dotnet run --project tests/WorkforceIntegrationGateway.IntegrationTests --no-build
```

The first restore after an intentional dependency update must omit
`--locked-mode` so the committed lock files can be regenerated and reviewed.
The integration suite uses an in-process ASP.NET Core test host at this stage;
it does not claim PostgreSQL durability.

## Run the API foundation

```powershell
dotnet run --project src/WorkforceIntegrationGateway.Api
```

The API listens on the URL reported by ASP.NET Core. Its current routes are:

- `POST /api/v1/verification-requests`
- `GET /api/v1/verification-requests/{id}`

Use [examples/verification-requests.http](examples/verification-requests.http)
for synthetic requests. The accepted public contract is recorded in
[documentation/openapi.yaml](documentation/openapi.yaml). The API is an
unauthenticated local demonstration and must not receive real personal data.
