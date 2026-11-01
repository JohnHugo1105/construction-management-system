# Testing

| Suite | Needs | Run |
| --- | --- | --- |
| `Cms.Domain.UnitTests` (domain rules, application logic) | nothing | `dotnet test tests/Cms.Domain.UnitTests` |
| `Cms.ArchitectureTests` (layering, tenant filters, money and date types, string lengths) | nothing | `dotnet test tests/Cms.ArchitectureTests` |
| `Cms.Api.Tests` (the real API on a real SQL Server) | SQL Server | `dotnet test tests/Cms.Api.Tests` |
| `Cms.Infrastructure.IntegrationTests` (queries, mappings, migrations) | SQL Server | `dotnet test tests/Cms.Infrastructure.IntegrationTests` |
| Angular unit tests (Vitest) | Node 22 | `npm test --prefix web -- --watch=false` |

## SQL Server for the integration tests

Set `CMS_TEST_SQLSERVER` to a connection string for a SQL Server 2022 instance **without a database name**. Each test class creates
its own uniquely named database and drops it afterwards. Without the variable those tests are reported as **skipped**, never as
passed.

```bash
docker compose -f deploy/docker-compose.yml up -d sql
export CMS_TEST_SQLSERVER="Server=localhost,1433;User Id=sa;Password=<the CMS_DB_PASSWORD from .env>;TrustServerCertificate=True"
dotnet test ConstructionManagementSystem.sln --configuration Release
```

CI runs the same with a SQL Server service container.

## What "tested" means here

A rule is covered when a test asserts the exact outcome: the centavo, the status code, the field that carries the error. Tests do not
assert that a call "doesn't throw". Money is compared in minor units. Every endpoint family has a test for a wrong role and for a
second company trying to reach the first company's data.

Not covered by automated tests, by design: real-phone behaviour, SQL Server failover, load. See `docs/operations/` for the manual
checks that stand in for them.
