# How to add a feature slice

A feature is one vertical slice through the four layers. Identity, Organizations, Projects and Audit are the reference slices:
copy their shape. A slice adds files in its own folders and **edits no shared file** (`Program.cs`, `ConstructOpxDbContext`, dependency
registration and endpoint registration all discover new classes by convention).

```
src/ConstructOPX.Domain/Features/<Feature>/            entities, value objects, rules (already written for every feature)
src/ConstructOPX.Application/Features/<Feature>/       <Feature>Contracts.cs (ports + DTOs + mapper), <Name>Service.cs
src/ConstructOPX.Infrastructure/Features/<Feature>/    <Feature>Configurations.cs, <Name>Repository.cs, <Name>Reader.cs
src/ConstructOPX.Api/Endpoints/<Feature>Endpoints.cs   one or more IEndpointModule classes
tests/ConstructOPX.Api.Tests/Features/<Feature>ApiTests.cs
tests/ConstructOPX.Domain.UnitTests/Features/...        only for domain rules or application logic you add
```

## Application layer

* A use case is a public sealed class named `*Service` (found and registered automatically). It takes ports through its constructor
  and returns DTOs. It never returns or accepts a domain entity across the API boundary.
* Ports are interfaces in the Application assembly: `*Repository` (loads and adds tracked aggregates) and `*Reader` (projects straight
  to DTOs in SQL for lists, reports and lookups; never hydrate whole aggregates to build a list). Infrastructure implements them in
  classes named `*Repository` and `*Reader` (registered automatically against the Application interfaces they implement).
* Take the person and organization from `ICurrentUser`, the time from `IClock`. Never read them from a request body.
* Every state-changing method: load the aggregate (a missing or other-company record is `NotFoundException`), call
  `ConcurrencyGuard.Ensure(entity.Version, request.Version)` on updates, call the domain method, `audit.Record(...)`, then
  `await unitOfWork.SaveChangesAsync(ct)` once, and return the DTO. The audit line and the change commit together or not at all.
* Creates that a double-click could repeat take an optional `ClientRequestId` (the web app sends a fresh GUID per form). Look the
  request id up first and return the original result; a filtered unique index is the backstop.
* Requests are records with nullable strings (`string?`) so the domain reports a friendly validation message instead of the JSON
  binder failing. Domain methods validate and throw `ValidationFailedException` listing every problem at once.

| Throw | Becomes | Use for |
| --- | --- | --- |
| `ValidationFailedException` (field to messages) | 422 with `errors` | bad input, reported per field |
| `DomainRuleException` | 409 `rule_violation` | a business rule forbids it right now (archived project, not enough stock) |
| `ConcurrencyConflictException` | 409 `concurrency_conflict` | the record changed since it was opened |
| `NotFoundException` | 404 | no such record in this company |

Messages are written for the end user (plain words, what happened, what to do next). Never put ids, SQL or exception text in them.

## DTOs and JSON

* camelCase; enums are strings (`"Active"`); dates are `DateOnly` (`"2026-11-02"`); timestamps are UTC `DateTimeOffset`.
* **Money is `long` minor units** in properties ending `Minor` (`grossMinor`: 1,250.50 pesos is `125050`). The currency comes from the
  session (`currencyCode`). Quantities are `decimal` with at most 4 decimals. No `double` or `float` anywhere.
* Every editable record's DTO carries `version`; every update, delete-like and status request carries the `version` the person opened.
* Lists return `PagedResult<T>` (`items`, `total`, `page`, `pageSize`), page size at most 100, with server-side search and filters.
* Never expose a raw id as something to type; give pickers a lookup endpoint that returns names.

## Infrastructure layer

* One `IEntityTypeConfiguration<T>` per entity in `<Feature>Configurations.cs` (picked up automatically).
* Every string has `HasMaxLength`; enums use `.HasConversion<string>().HasMaxLength(n)`; quantities `HasPrecision(18, 4)`; money is
  `long` (bigint); dates are `DateOnly` (`date`) and instants `DateTimeOffset`. Never store money or dates as text.
* Every entity has a `TenantId` (the tenant query filter is applied automatically) and aggregates derive from `AggregateRoot`
  (the `Version` concurrency token is applied automatically).
* Add indexes for the queries you wrote. Back every service-level uniqueness check with a unique index (filtered when partial).
* Use `Sql.Contains(text)` with `EF.Functions.Like(column, pattern, Sql.LikeEscape)` for search, so `%` and `_` are literal.
* **Do not add a migration.** One migration for the whole model is generated at integration time. Tests build the database from the
  model until then.
* Count SQL statements on read paths you touch: they must stay constant as rows grow.

## API layer

* Implement `IEndpointModule` and map under the `/api` group you are given: `api.MapFeature("/things", "Things")`.
* **Every endpoint ends with `.RequirePermission(Permissions.X)`** (the matrix is `ConstructOPX.Application.Common.Permissions`). Architecture
  tests fail an endpoint without one. Writes are protected by the anti-forgery filter on the group automatically.
* `POST` that creates returns `Results.Created(location, dto)`; other writes return `Results.Ok(dto)` or `Results.NoContent()`.
* Add no exception handling in endpoints; the central handler maps exceptions.

## Tests

* API tests use `[SqlServerFact]`, `IClassFixture<ConstructOpxApiFactory>`, `TenantSeeder.CreateTenantAsync` (a private company per test) and
  `ApiClient` (cookies and anti-forgery handled like the browser). Set `CONSTRUCTOPX_TEST_SQLSERVER` (see `testing.md`).
* Cover, for every endpoint family: the happy path, validation errors per field, a stale `version` (409), a wrong role (403), a
  second company reading or changing your data (404), and each business rule you added. Assert exact figures for money.
* Run only your tests while working: `dotnet test tests/ConstructOPX.Api.Tests --filter "FullyQualifiedName~YourFeature"`.
* Before you finish also run `dotnet test tests/ConstructOPX.ArchitectureTests` and `dotnet test tests/ConstructOPX.Domain.UnitTests`.
* When your endpoints land, delete your permissions from `ExpectedUnusedWhileBuilding` in `EndpointSecurityTests`.
