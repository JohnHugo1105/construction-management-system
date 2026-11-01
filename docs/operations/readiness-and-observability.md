# Readiness and observability

## Health endpoints (no sign-in)

| Endpoint | Answers | Use it for |
| --- | --- | --- |
| `GET /health/live` | The process is up. It checks nothing else. | Container "is it running" probes |
| `GET /health/ready` | The database answers **and** no migration is pending (200), otherwise 503 with `database` and `migrations`. | Load balancer routing, deploy gates |
| `GET /health/version` | `service`, `version`, `commit` (`CONSTRUCTOPX_RELEASE_SHA`), `environment`, `runtime`. | Proving which build is running |

Through the web server (nginx) only `/health/live` answers from outside; `/health/ready` and `/health/version` answer only to callers on a
private network (10/8, 172.16/12, 192.168/16, loopback), because they reveal the database state and the release. Probe the API
container directly, or from a private address.

A deploy is healthy only when `/health/ready` is 200 and `/health/version` shows the commit that was deployed. A healthy check proves
only what it names: it does not prove sign-in or calculations work; do the smoke test.

## Errors

Every error response is RFC 9457 problem details with a `traceId` and, for validation, per-field `errors`. Anything unexpected becomes
a generic message plus the `traceId`; **exception text, SQL and stack traces never leave the server.** The same id is returned in the
`X-Correlation-Id` header and is on every log line written while the request ran, with the company and user ids. A person quotes the
reference; you find the log line by it.

## Logging

Structured logs to standard output with `CorrelationId`, `TenantId` and `UserId` in scope. Passwords, pay amounts and tokens are never
logged. Ship them to your log service; keep identifiers in local logs and strip them before any third-party export.

## What to watch

API and web health; `/health/ready` failing; the 5xx rate; sign-in 401 and 429 spikes; slow requests; database size and free disk
(warn at 70%, urgent at 85%); backup older than 26 hours; restore-check failures; certificate expiry. Alerts need a named owner.
Latency numbers taken on a laptop are diagnostic only: do not publish them as capacity.

## Cadence

Per release: health and version checks, smoke test. Daily: confirm the backup. Weekly: restore check, capacity glance. Monthly:
access and secret review. Quarterly: recovery drill on a replacement server (until done, no recovery-time claim is made).
