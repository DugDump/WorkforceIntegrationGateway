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
- `WorkforceIntegrationGateway.Application` owns the verification-request use case and persistence port.
- `WorkforceIntegrationGateway.Domain` owns the canonical request model and its invariants.
- `WorkforceIntegrationGateway.Infrastructure` implements PostgreSQL persistence and identifier generation.
- `WorkforceIntegrationGateway.UnitTests` verifies focused domain and application behavior.
- `WorkforceIntegrationGateway.IntegrationTests` verifies the HTTP contract, PostgreSQL mapping, migrations, failures, and restart durability.
- `WorkforceIntegrationGateway.ArchitectureTests` enforces production project references.
- `WorkforceIntegrationGateway.TestReport` verifies that CI discovered and executed every required test without skips.

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
`WIG_POSTGRES_PORT` and the connection string together. The `dotnet-ef` command
requires `ConnectionStrings__WorkforceGateway`; it never selects a fallback
database. For API runtime only, user-secrets can hold the same setting without
committing it:

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

## Demonstrate the first deliverable

Keep the database and API running, then execute the requests in
`examples/verification-requests.http` in the following order:

1. Run **Create a synthetic verification request**. Confirm the response is
   `201 Created`, contains an application-assigned `id` and `"status":
   "pending"`, and includes a `Location` response header.
2. Copy the returned `id` into **Retrieve the created request** and run it.
   Confirm the response is `200 OK` and its body corresponds to the created
   request.
3. Run **Reject an invalid request**. Confirm the response is `400 Bad Request`
   with an `application/problem+json` body. Then verify that the invalid client
   reference created no database row:

   ```powershell
   docker compose exec database psql --username wig_synthetic --dbname wig_synthetic --tuples-only --no-align --command "SELECT COUNT(*) FROM gateway.verification_requests WHERE client_reference = 'invalid reference with spaces';"
   ```

   The command must print `0`. If you changed `WIG_POSTGRES_USER` or
   `WIG_POSTGRES_DB` from `.env.example`, use those values instead.
4. Run **Report an unknown, well-formed identifier**. Confirm the response is
   `404 Not Found` with an `application/problem+json` body.
5. Stop the API with Ctrl+C, leaving the Compose database running. Start the
   API again with the same `dotnet run` command and connection string.
6. Run **Retrieve the created request** again with the original `id`. Confirm
   it still returns `200 OK` with the same representation, demonstrating that
   PostgreSQL preserved the resource across the API restart.

The supplied HTTP file targets `http://localhost:5169`, the HTTP address in the
checked-in launch profile. If the startup output reports a different address,
change `baseUrl` in the HTTP file to that address before running the requests.
