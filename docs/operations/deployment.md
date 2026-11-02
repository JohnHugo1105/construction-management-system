# Deployment

One immutable image per approved commit, deployed by exact commit id. Images: `constructopx-api:<sha>` (`src/ConstructOPX.Api/Dockerfile`) and
`constructopx-web:<sha>` (`deploy/web/Dockerfile`). `deploy/docker-compose.yml` runs SQL Server, the API and the web server.

> **Verification status.** The Dockerfiles, nginx configuration and compose file were written and reviewed but **not built or run**
> in the environment where this repository was authored (no Docker daemon was available). The API, the migrations and the
> health endpoints were run and tested directly (see `docs/development/testing.md`). Build the images once in CI or on a build
> machine and run the smoke checks below before relying on them.

## Topology

```
client ──TLS──> reverse proxy / load balancer ──> web (nginx: static files, /api and /health proxied) ──> api (:8080) ──> SQL Server
```

* TLS ends at the proxy, which **must send `X-Forwarded-Proto`** so the API issues `Secure` cookies and anti-forgery tokens, and
  `X-Forwarded-For` so the sign-in rate limiter and the activity log see the person's address, not the proxy's. nginx trusts those
  headers only from private networks (`set_real_ip_from` in `deploy/web/nginx.conf`); add the proxy's address there if it is public.
* Only the web container publishes a port. The API and the database are on the private network; SQL Server is never exposed. **The API
  must stay reachable only through nginx**: it takes the client address from the one value nginx sets, so a client that could reach the
  API directly could choose its own address and slip past the rate limiter.
* `/health/live` is public. `/health/ready` and `/health/version` (database state, release id) answer only inside the private
  network; check them from the host or a monitor, not from the internet.
* The API is stateless (cookie sessions are re-validated against the database), so more than one API instance is safe. Background
  work does not exist yet; when it does, make each job single-instance before scaling out.

## First installation

1. Copy `.env.example` to `.env` and replace every `REPLACE-ME` (a copy that was never edited refuses to start). Two passwords: the SQL
   Server administrator (`CONSTRUCTOPX_DB_PASSWORD`, for migrations and backups only) and the API's own login (`CONSTRUCTOPX_APP_DB_PASSWORD`).
2. `docker compose -f deploy/docker-compose.yml up -d sql db-init`: starts SQL Server, then creates the `ConstructOpx` database and the
   `constructopx_app` login (read and write the tables, no schema changes; `deploy/sql/create-app-login.sql`).
3. Apply the reviewed migration script as the administrator (`docs/operations/database-and-migrations.md`).
4. Set `CONSTRUCTOPX_BOOTSTRAP_ENABLED=true`, `docker compose -f deploy/docker-compose.yml up -d --build`, open the site and create the company
   and first administrator, then set it back to `false` and restart the API.

## Release steps

1. **Pick the commit** that passed CI. Record its full SHA.
2. **Build** both images tagged with the SHA (`--build-arg CONSTRUCTOPX_RELEASE_SHA=<sha>`); the API reports the SHA at `/health/version`, the
   web server at `/version.json`.
3. **Back up** the database and, if the release has a migration, review and apply the script
   (`docs/operations/database-and-migrations.md`).
4. **Start the candidate** (`CONSTRUCTOPX_RELEASE_SHA=<sha> docker compose -f deploy/docker-compose.yml up -d --no-build`).
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
`CONSTRUCTOPX_DATABASE_CONNECTION`, `CONSTRUCTOPX_RELEASE_SHA`, `Bootstrap__Enabled` (turn on only for first-time setup, then off),
`PasswordHashing__Iterations`, `RateLimiting__SignInPerMinute`, `Security__RequireSecureCookies` (leave true outside local HTTP),
`DataProtection__KeysPath` (a persistent private folder for the session keys: compose mounts the `api-keys` volume there; without
it, every restart signs everyone out and the API logs a warning. With more than one API instance, point them all at one shared folder).
`FileStorage__RootPath` (a persistent private folder for uploaded project documents: compose mounts the `api-files` volume at
`/var/lib/constructopx/files`; empty means file storage is not set up, uploads are refused in plain words and people add links instead.
With more than one API instance, point them all at one shared folder), `FileStorage__MaxFileMegabytes` (default 25),
`FileStorage__AllowedExtensions` (optional, narrows the built-in list) and `FileStorage__UploadsEnabled` (default true; set false to stop
uploads in an emergency, files already stored still download). The Administration > System readiness page shows whether file storage works.
First-time setup: set `CONSTRUCTOPX_BOOTSTRAP_ENABLED=true`, start, open the site, create the company and first administrator, then set it back
to `false` and restart.

## Scaling and cost

Scale on a measured trigger (sustained CPU or memory pressure, slow requests, a database near its limits), and write the trigger down.
Do not change the database engine and the host in the same step.
