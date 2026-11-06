# Testing

| Suite | Needs | Run |
| --- | --- | --- |
| `ConstructOPX.Domain.UnitTests` (domain rules, application logic) | nothing | `dotnet test tests/ConstructOPX.Domain.UnitTests` |
| `ConstructOPX.ArchitectureTests` (layering, tenant filters, money and date types, string lengths) | nothing | `dotnet test tests/ConstructOPX.ArchitectureTests` |
| `ConstructOPX.Api.Tests` (the real API on a real SQL Server) | SQL Server | `dotnet test tests/ConstructOPX.Api.Tests` |
| `ConstructOPX.Infrastructure.IntegrationTests` (queries, mappings, migrations) | SQL Server | `dotnet test tests/ConstructOPX.Infrastructure.IntegrationTests` |
| Angular unit tests (Vitest) | Node 22 | `npm test --prefix web -- --watch=false` |

## SQL Server for the integration tests

Set `CONSTRUCTOPX_TEST_SQLSERVER` to a connection string for a SQL Server 2022 instance **without a database name**. Each test class creates
its own uniquely named database and drops it afterwards. Without the variable those tests are reported as **skipped**, never as
passed.

```bash
docker compose -f deploy/docker-compose.yml up -d sql
export CONSTRUCTOPX_TEST_SQLSERVER="Server=localhost,1433;User Id=sa;Password=<the CONSTRUCTOPX_DB_PASSWORD from .env>;TrustServerCertificate=True"
dotnet test ConstructOPX.sln --configuration Release
```

CI runs the same with a SQL Server service container.

## What "tested" means here

A rule is covered when a test asserts the exact outcome: the centavo, the status code, the field that carries the error. Tests do not
assert that a call "doesn't throw". Money is compared in minor units. Every endpoint family has a test for a wrong role and for a
second company trying to reach the first company's data.

Not covered by automated tests, by design: real-phone behaviour, SQL Server failover, load. See `docs/operations/` for the manual
checks that stand in for them.

## Time clock: what is proven and what is not (rules: business-rules.md section 17)

Proven by tests (domain unit tests, API tests on a real SQL Server, Angular specs):

* The device scheme: a hashed key compared in constant time, the tenant taken only from the device, identical 401s for unknown, revoked,
  rotated-out and malformed keys, 429 over 120 requests a minute per key, the machine route refusing a person's cookie, and a device key
  refused (401) on a sample of 16 other routes. The endpoint security test allows exactly one route with the device policy.
* Idempotency and offline replay, the clock bounds, identical answers for unknown, replaced and inactive badges, the kiosk answer carrying only
  name, direction and time (a name walk of the JSON for pay, money, code, key, photo and place words), the 60-second rule, automatic
  directions, the Manila date differing from the UTC date and the 04:00 day-start, pairing, the break, rounding with awkward minutes, the cap
  at paid hours, overtime proposed and never recorded until approved, night hours, each exception, a worker not posted.
* Confirmation through the attendance rules, never overwriting typed attendance, idempotent, "Confirm all clean rows" skipping exceptions and
  unapproved overtime, and **the same pay for the same hours typed by hand and confirmed from the clock** (a payroll run over both).
* Tenant isolation for devices, scans, workers, projects, badges and the import; role refusals for every permission boundary.
* A fixed number of SQL statements: the device push costs 10 statements for 1 scan and for 10 (the device's key, the device, its last-seen
  mark, the company, the project, the scan ids, the workers, the day's scans, the postings, one insert); the review list costs 6 for 1
  worker and for 10. EF Core batches inserts 42 rows to a command, so a push of 200 new scans adds a few insert commands (per 42 scans, not
  per scan).
* The Code 128 drawing matches an independent implementation (JsBarcode 3.11.6) for three codes, and every symbol in its table was checked
  against that implementation's table; the QR code comes from the bundled library.

Not proven:

* **No real time clock machine or scanner was ever connected.** The keyboard-style USB or Bluetooth scanner is simulated in the kiosk spec by
  key events (each character, then Enter). A physical scanner's speed, its suffix key (some send Tab, not Enter) and its handling of the QR
  code were not tested; nor was a printed badge scanned. Scanners are commonly configured to send Enter; check it when one is set up.
* The kiosk's beep, the A4 print layout and the badge size on paper were not checked on a printer or a tablet (browser specs only, not
  Android).
* No vendor protocol, no load test beyond the fixed statement counts, and the rate limit is per server process (several servers would each
  allow 120 a minute).
