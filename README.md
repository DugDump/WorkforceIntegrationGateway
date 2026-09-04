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
`Pending` state. PostgreSQL stores accepted requests durably across API restarts.
Provider integration, authentication, asynchronous processing, deployment, and
production readiness are not implemented yet.

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
- Docker Desktop using Linux containers and Docker Compose v2.

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
The required integration suite starts the repository-pinned PostgreSQL image
through Testcontainers. Docker unavailability fails the suite; it never skips or
substitutes another provider.

## Start and migrate the development database

Copy `.env.example` to the ignored `.env`, replace the password placeholder,
and choose a free host port if `5432` is already occupied. Then run:

```powershell
docker compose up -d --wait --wait-timeout 65 database
dotnet tool restore
$env:ConnectionStrings__WorkforceGateway = "Host=localhost;Port=5432;Database=wig_synthetic;Username=wig_synthetic;Password=<your-local-password>"
dotnet tool run dotnet-ef database update --project src/WorkforceIntegrationGateway.Infrastructure
```

`database update` is repeatable and applies only unapplied committed migrations.
The API validates configuration, connectivity, and schema currency at startup;
it never applies migrations itself. A port collision requires changing
`WIG_POSTGRES_PORT` and the connection string together. As an alternative to an
environment variable, configure the same setting without committing it:

```powershell
dotnet user-secrets set "ConnectionStrings:WorkforceGateway" "Host=localhost;Port=5432;Database=wig_synthetic;Username=wig_synthetic;Password=<your-local-password>" --project src/WorkforceIntegrationGateway.Api
```

Stop the database while retaining its project-owned volume:

```powershell
docker compose down
```

If startup was interrupted, the same command removes only resources in this
Compose project. Destructive reset (deletes only the project-owned database
volume and all local demonstration data):

```powershell
docker compose down --volumes
```

## Run the API

```powershell
dotnet run --project src/WorkforceIntegrationGateway.Api
```

The API listens on the URL reported by ASP.NET Core. Its current routes are:

- `POST /api/v1/verification-requests`
- `GET /api/v1/verification-requests/{id}`

Use [examples/verification-requests.http](examples/verification-requests.http)
for synthetic requests. The accepted public contract is recorded in
[documentation/openapi.json](documentation/openapi.json). The API is an
unauthenticated local demonstration and must not receive real personal data.
