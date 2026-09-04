# Workforce Integration Gateway

Workforce Integration Gateway is a fictional ASP.NET Core reference service for
demonstrating production-oriented .NET backend engineering. It will accept
synthetic employment-verification requests and, in later increments, integrate
with simulated payroll and HR providers. It has no affiliation with an employer
or verification provider and must never contain real employee data or Social
Security numbers.

## Current status

The repository currently provides the buildable .NET 10 solution foundation.
No verification-request API, database persistence, provider integration,
authentication, asynchronous processing, deployment, or production-readiness
claim is implemented yet.

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
```

The first restore after an intentional dependency update must omit
`--locked-mode` so the committed lock files can be regenerated and reviewed.
Unit and integration test executables are added as their governed behavior is
implemented; empty test projects are not presented as successful evidence.

## Run the API foundation

```powershell
dotnet run --project src/WorkforceIntegrationGateway.Api
```

The foundation starts an empty API host and exposes development OpenAPI metadata.
Product endpoints arrive as separately reviewable increments.
