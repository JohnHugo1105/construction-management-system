# Construction Management System

Budget and cost control, workforce and payroll, and warehouse stock for a construction company, as a web application.

It replaces the earlier Windows desktop program (WinForms and MySQL/SQL Express, last present at commit `93da838`) and is built on the
DriveOPX stack: **Angular 22**, **ASP.NET Core 10** Minimal APIs, **EF Core 10**, **SQL Server 2022**, as a monolith-first Clean
Architecture. The engineering standards come from DriveOPX and are in [AGENTS.md](AGENTS.md); how they were adapted is in
[docs/development/standards-deviations.md](docs/development/standards-deviations.md).

## What it does

| Area | In plain words |
| --- | --- |
| **Projects** | Projects and sub-projects with a lifecycle (planning, active, on hold, completed), a contract price, archive with a reason, and restore. |
| **Budget and cost** | Budget lines and actual cost entries classified by the standard 16-division work breakdown, with exact VAT (included, excluded or none), duplicate-invoice protection, void instead of delete, and budget against actual: remaining, percentage used and health (on track, near limit, over budget, unbudgeted). |
| **Dashboard and exports** | Every project's budget, actual, remaining and margin in one view; a cost summary per project; CSV exports. |
| **Workforce** | Workers, effective-dated pay history that can't be rewritten, postings to projects, status changes, cases. Pay is visible only to people who run payroll. |
| **Attendance and payroll** | Daily attendance, pay calculated at the rate in force each day (regular and overtime), deductions, approval that posts labor cost to each project, voiding that reverses it. |
| **Stock** | Tools, equipment and machinery in the warehouse and on projects, an append-only ledger with no negative stock, reversals, and optional charging of a transfer to a project. |
| **Administration** | Users and roles, company settings (VAT rate, time zone, paid hours, overtime rate), and an activity log of every change. |

The exact rules, and the 25 defects found in the desktop program and how each was corrected, are in
[docs/business-rules.md](docs/business-rules.md).

## Run it locally

You need the .NET 10 SDK (the version in `global.json`), Node 22, and a SQL Server 2022 (Docker is the easiest).

```bash
cp .env.example .env                                  # then edit CMS_DB_PASSWORD (SQL Server needs a strong password)
docker compose -f deploy/docker-compose.yml up -d sql

export CMS_DATABASE_CONNECTION="Server=localhost,1433;Database=Cms;User Id=sa;Password=<CMS_DB_PASSWORD>;TrustServerCertificate=True"
export Seeding__Enabled=true                          # optional demo company; development only, empty database only
dotnet run --project src/Cms.Api                      # http://localhost:5100 (creates the database on first start)

npm ci --prefix web
npm start --prefix web                                # http://localhost:4200, proxies /api to the API
```

With demo data, sign in as `admin`, `pm`, `cost`, `accounts`, `hr`, `store` or `viewer`; every demo account's password is
`Demo-passw0rd-2026` (development only: the seeder refuses to run outside Development). Without demo data, the first visit offers
first-time setup, which creates your company and administrator (turn `Bootstrap:Enabled` off afterwards in production).

## Repository map

```
src/Cms.Domain          entities, value objects and business rules (no dependencies)
src/Cms.Application     use cases, ports, DTOs, the permission matrix
src/Cms.Infrastructure  EF Core and SQL Server, password hashing, repositories and readers, migrations, development seeder
src/Cms.Api             Minimal API endpoints, cookie sessions, error handling, health checks, composition root
web/                    Angular 22 client (core, shared, features)
tests/                  domain, API, integration, architecture tests
docs/                   business rules, architecture, development guides, operations runbooks
deploy/  scripts/       compose and nginx files; backup, restore-check and source-guard scripts
.agents/ .claude/       DriveOPX engineering references, subagents and UI audit skills (see CLAUDE.md)
```

## Testing

```bash
dotnet test tests/Cms.Domain.UnitTests          # rules, no database
dotnet test tests/Cms.ArchitectureTests         # layering, tenant filters, money and date types, permissions on every endpoint
export CMS_TEST_SQLSERVER="Server=localhost,1433;User Id=sa;Password=<CMS_DB_PASSWORD>;TrustServerCertificate=True"
dotnet test ConstructionManagementSystem.sln    # adds the API and database tests (skipped, never "passed", without the variable)
npm test --prefix web -- --watch=false
npm run validate                                # everything, as CI does
```

See [docs/development/testing.md](docs/development/testing.md) and
[docs/development/vertical-slice-guide.md](docs/development/vertical-slice-guide.md) (how to add a feature).

## Operating it

[docs/operations/](docs/operations): [deployment](docs/operations/deployment.md),
[database and migrations](docs/operations/database-and-migrations.md),
[backup and restore](docs/operations/backup-and-restore.md),
[secrets and access](docs/operations/secrets-and-access.md),
[readiness and observability](docs/operations/readiness-and-observability.md).
Architecture and the decision log: [docs/architecture/overview.md](docs/architecture/overview.md).

> **Security note.** The retired desktop program's git history contains a signing key, a database export and commented-out connection
> strings with credentials. Treat anything that was ever real as exposed and rotate it
> ([docs/operations/secrets-and-access.md](docs/operations/secrets-and-access.md)).
