# Architecture overview

A monolith-first Clean Architecture application, on the DriveOPX stack: Angular 22 client, ASP.NET Core 10 Minimal API, EF Core 10,
SQL Server 2022. One deployable API and one static web client; features are folders, not services.

```
web/ (Angular 22)                        src/
  core/    session, auth, shell            Cms.Domain          entities, value objects, rules             (no dependencies)
  shared/  ui kit, money/date helpers      Cms.Application     use cases, ports, DTOs, permissions       (Domain)
  features/<feature>/                      Cms.Infrastructure  EF Core, SQL Server, hashing, adapters   (Application, Domain)
        *-api.ts  (only HTTP here)         Cms.Api             Minimal APIs, auth, errors, composition   (Application, Infrastructure)
```

Dependencies point inward and `tests/Cms.ArchitectureTests` enforces it: the inner layers have no package references and no EF Core,
ASP.NET Core or SQL client namespaces, money is never floating point, every tenant-owned entity has the tenant filter, every aggregate has
a concurrency token, every string column has a length, and every API endpoint outside identity names a permission.

## Features

| Feature | Domain model | What it owns |
| --- | --- | --- |
| Identity | `User`, `AccessRole`, `PasswordPolicy` | Sign-in, lockout, first-time setup, user administration |
| Organizations | `Organization` | The company: VAT rate, currency, time zone, paid hours, overtime multiple, variance basis |
| Projects | `Project`, `SubProject` | Lifecycle, archive and restore, contract price |
| Costing | `BudgetLine`, `CostEntry`, `LineAmounts`, `CostClassification`, `WbsCatalog`, `BudgetPosition` | Budget and actual cost, VAT, classification, variance, dashboard, exports |
| Workforce | `Worker`, `WorkerRate`, `WorkerAssignment`, `WorkerCase` | People, pay history, postings, cases |
| Payroll | `AttendanceEntry`, `PayrollRun`, `PayrollCalculator` | Attendance, pay calculation, approval, labor cost posting |
| Inventory | `InventoryItem`, `StockBalance`, `StockMovement` | Tools, equipment, machinery, stock ledger, charged transfers |
| Suppliers | `Supplier` | The firms the company buys from and hires |
| Purchasing | `PurchaseOrder` | Purchase orders, invoices matched to order lines, the open commitment |
| Progress | `ProgressEntry`, earned-value calculator | Percent complete per budget line and the earned-value figures |
| Change orders | `ChangeOrder`, the project's budget baseline | Approved changes to budget and contract price |
| Billing | `ProjectBillingTerms`, `ClientInvoice`, `ClientPayment` | Bills, retention, payments received |
| Subcontracts | `Subcontract`, claims, payments | Awards, certified claims posted to cost, payments made |
| Statutory | `StatutorySchedule`, `StatutoryCalculator` | Contribution and tax tables and the deductions they give |
| Audit | `AuditEntry` | The append-only activity log |

`docs/business-rules.md` is the authoritative statement of the rules each of these enforces.

## A request, end to end

1. The Angular service (`*-api.ts`) calls `/api/...`. A session cookie and, for writes, the `X-XSRF-TOKEN` header travel with it.
2. The API pipeline: exception handler, security headers, forwarded headers, routing, rate limiter (sign-in), authentication (the cookie's
   principal is **re-checked against the database** for an active account, unchanged roles and an unchanged security stamp, so a
   copied cookie dies when the person signs out, changes a password or is turned off), correlation id, "password change
   required" gate, authorization (a named permission per endpoint), then the endpoint filters (private/no-store, anti-forgery on writes).
3. The endpoint calls one application service. The service takes the person and company from `ICurrentUser`, loads aggregates through
   repositories (the tenant filter is added by `CmsDbContext` to every query), calls domain methods that validate and enforce rules,
   records an audit line and saves once through `IUnitOfWork`.
4. Domain exceptions become problem details in one place (`ApiExceptionHandler`): validation (422, per field), rule violation
   (409), stale version (409 `concurrency_conflict`), not found (404), sign-in failure (401). Unexpected errors become a generic message
   with a trace id; internals never reach the client.

## Multi-company isolation (three layers)

1. Every entity carries `TenantId` and `CmsDbContext` adds `WHERE TenantId = <signed-in company>` to every query (nobody signed in
   means no rows).
2. Domain constructors stamp the tenant from the current user; the context **refuses to save** a new record stamped with another
   company's id.
3. Tests sign in as two companies and try to read and change each other's records through every endpoint family; the answer is always 404.

Usernames are unique across the whole system because sign-in has no company field.

## Concurrency

Every aggregate has an integer `Version` that `CmsDbContext` increments on each update; it is also an EF concurrency token. Every
update request carries the version the person opened. The service compares it first (the token alone can't catch a stale form), and the
token catches two saves racing in the same instant. Either way: 409 `concurrency_conflict` and the person is asked to reload.

## Decision log

| Decision | Why |
| --- | --- |
| Money as `long` minor units, `decimal` quantities | Exact arithmetic; the retired app's `double` text money drifted. A currency is a company setting, so lines don't repeat it. |
| `net + VAT = gross` by construction | Compute one rounded base, derive the rest, so no total ever disagrees with its parts. |
| Cookie sessions with anti-forgery, not JWT | The browser client is first-party; an HttpOnly, SameSite=Strict cookie plus a per-identity anti-forgery token is simpler and safer than tokens in script-readable storage, and a session can be revoked at once: the account and a per-user security stamp are re-checked each request (15 s cache), and signing out, a password change or turning the account off changes the stamp. Signing out therefore signs the person out on every device. |
| Integer `Version` instead of `rowversion` | Portable, visible in DTOs for the explicit stale-form check, and testable on any provider. DriveOPX uses integer revisions in most places too. |
| EF global query filters for tenancy | A forgotten `Where` in a new query can't leak data. Explicit `IgnoreQueryFilters()` appears only in sign-in and first-time setup. |
| No MediatR, no CQRS framework | Services per use case, and `*Reader` ports for projected reads, give the same separation with less indirection (YAGNI, per AGENTS.md). |
| Commitments are a port, not a table | Open purchase orders and subcontracts each implement `ICommitmentSource`; the report service adds every source's remainder to the actual cost to give the forecast. A new kind of commitment adds a class, and nothing is counted twice because a source subtracts what has already become actual cost. |
| Atomic numbering, gaps allowed | `INumberSequence` hands out the next number with one SQL statement, so two people never get the same purchase order or bill number. A save that fails after taking a number leaves a gap: numbers are unique and rising, not unbroken. |
| Statutory rates are data, never code | Contribution and tax tables are effective-dated rows an accountant enters; nothing in the code or the seed claims a legal rate. Until a table exists the feature says it needs setup and payroll falls back to manual deductions. |
| Void and archive, never delete | Financial history must stay explainable; deleting orphaned the retired app's data. |
| Generated migrations only; a development database may be created from the model until the first migration exists | One reviewed history of the schema; no hand edits in production. |
| Pay data is gated by a permission, not a UI hint | The API omits pay from responses for people without `payroll.read`. |
