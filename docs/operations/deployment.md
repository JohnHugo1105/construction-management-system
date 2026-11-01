# Deployment

One immutable image per approved commit, deployed by exact commit id. Images: `cms-api:<sha>` (`src/Cms.Api/Dockerfile`) and
`cms-web:<sha>` (`deploy/web/Dockerfile`). `deploy/docker-compose.yml` runs SQL Server, the API and the web server.

> **Verification status.** The Dockerfiles, nginx configuration and compose file were written and reviewed but **not built or run**
> in the environment where this repository was authored (no Docker daemon was available). The API, the migrations and the
> health endpoints were run and tested directly (see `docs/development/testing.md`). Build the images once in CI or on a build
> machine and run the smoke checks below before relying on them.

## Topology

```
client ──TLS──> reverse proxy / load balancer ──> web (nginx: static files, /api and /health proxied) ──> api (:8080) ──> SQL Server
```

* TLS ends at the proxy, which **must send `X-Forwarded-Proto`** so the API issues `Secure` cookies and anti-forgery tokens.
* Only the web container publishes a port. The API and the database are on the private network; SQL Server is never exposed.
* The API is stateless (cookie sessions are re-validated against the database), so more than one API instance is safe. Background
  work does not exist yet; when it does, make each job single-instance before scaling out.

## Release steps

1. **Pick the commit** that passed CI. Record its full SHA.
2. **Build** both images tagged with the SHA (`--build-arg CMS_RELEASE_SHA=<sha>`); the API reports the SHA at `/health/version`, the
   web server at `/version.json`.
3. **Back up** the database and, if the release has a migration, review and apply the script
   (`docs/operations/database-and-migrations.md`).
4. **Start the candidate** (`CMS_RELEASE_SHA=<sha> docker compose -f deploy/docker-compose.yml up -d --no-build`).
5. **Check**: `GET /health/live` 200; `GET /health/ready` 200 (`database: ok`, `migrations: current`); `GET /health/version` shows
   the SHA you deployed, and so does `/version.json` on the web server. A deploy is healthy only when both report the commit that was
   deployed.
6. **Smoke test** by hand: sign in, open the dashboard, open a project.
7. **Record**: SHA, who approved, when (UTC), the migration decision, and the previous tags for rollback.

## Rollback

Keep the previous image tags. If readiness or version checks fail after a deploy, start the previous tags. A rollback does **not**
undo a migration; additive migrations are compatible with the previous version, which is why they are required. An untested rollback
is labelled untested in the release record.

## Configuration

Everything comes from environment variables (see `.env.example`); nothing secret is in the image or the repository.
`CMS_DATABASE_CONNECTION`, `CMS_RELEASE_SHA`, `Bootstrap__Enabled` (turn on only for first-time setup, then off),
`PasswordHashing__Iterations`, `RateLimiting__SignInPerMinute`, `Security__RequireSecureCookies` (leave true outside local HTTP).
First-time setup: set `CMS_BOOTSTRAP_ENABLED=true`, start, open the site, create the company and first administrator, then set it back
to `false` and restart.

## Scaling and cost

Scale on a measured trigger (sustained CPU or memory pressure, slow requests, a database near its limits), and write the trigger down.
Do not change the database engine and the host in the same step.
