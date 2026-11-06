# Standards applied from DriveOPX, and where this repository differs

Source: the DriveOPX repository (`AGENTS.md`, `CLAUDE.md`, `.agents/references/`, `.claude/agents/`, `.claude/skills/`,
`Directory.Build.props`, `global.json`, `.editorconfig`, `dotnet-tools.json`, `scripts/check-*.mjs`) and the DriveOPX
Drive handbooks. This page records what was carried over, what was adapted, and what was left out on purpose.

## Carried over unchanged in substance

| Standard | Where it lives here |
| --- | --- |
| Monolith-first Clean Architecture: Domain, Application, Infrastructure, Api, with feature folders inside each layer; dependencies point inward | `AGENTS.md` sections 5 and 12, `tests/ConstructOPX.ArchitectureTests` |
| Stack: Angular 22, ASP.NET Core 10 Minimal APIs, EF Core 10, SQL Server 2022 | `README.md`, `global.json`, `Directory.Build.props` |
| OOP, SOLID, DRY, KISS, YAGNI, DDD vocabulary; Decision Priorities; first-principles reasoning; independent judgment | `AGENTS.md` |
| Small / Normal / Complex tiers, delivery evidence, independent final review for Complex work | `AGENTS.md` section 15 |
| One-pass delivery flow, minimum-affected-tests posture, default performance check with a `Performance:` line | `AGENTS.md`, `.agents/references/performance-engineering-standards.md` |
| "Ship capabilities on, and guard what is not ready" | `AGENTS.md` |
| "Show less at once, lead to one next step", UI/UX standards, WCAG 2.2 AA, 375 x 812 first, no gradients | `AGENTS.md`, `.agents/references/ui-ux-design-standards.md`, `scripts/check-no-gradients.mjs` |
| Angular guidance: standalone, Signals, `inject()`, feature-first folders, `*-api.ts` transport, Vitest | `AGENTS.md` section 10, `web/README.md` |
| Minimal APIs with endpoint groups, anti-forgery on writes, central problem-details mapping, `private, no-store` | `AGENTS.md` section 11 |
| Money as minor units, void instead of delete, `ClientRequestId` idempotency, UTC `DateTimeOffset`, tenant filtering everywhere | `AGENTS.md` sections 6, 7, 9 |
| PBKDF2 password hashing, cookie sessions, rate-limited sign-in | `AGENTS.md` section 9 |
| NuGet lock files (`RestorePackagesWithLockFile`), architecture tests that locate the repo root through `AGENTS.md` | `Directory.Build.props`, `tests/ConstructOPX.ArchitectureTests` |
| Claude subagent roles and the "Opus decides, Sonnet builds, Haiku extracts" routing | `.claude/agents/`, `CLAUDE.md` |
| `ui-presentation-audit` and `ui-ux-pro` skills | `.claude/skills/` |

## Adapted

| DriveOPX | Here | Why |
| --- | --- | --- |
| Product personas: Renter, Host, Partner, Platform | Administrator, Project Manager, Cost Engineer, Site Engineer, HR Officer, Warehouse Keeper, Accountant | Different product |
| CRLF line endings | LF everywhere (`.editorconfig`, `.gitattributes`); `.cmd` and `.bat` stay CRLF | DriveOPX is developed only on Windows. This repository is built on Windows and Linux (CI, cloud sessions), and mixed endings create churn |
| `global.json` pins SDK 10.0.302 | Pins 10.0.403 | The version verified to build this repository |
| Lock files enforced locally | `RestoreLockedMode` and `TreatWarningsAsErrors` only when `CI=true` | Keeps the inner loop fast while CI stays strict |
| Angular Material mentioned in `AGENTS.md` | Not used; small shared UI primitives under `web/src/app/shared/ui` | DriveOPX's own `package.json` does not depend on it either; the written rule and the shipped code disagree there |
| "PR first, squash-merge when done" using `gh` on a Windows PC | Commit task-related files, push to the designated branch, open a PR or merge only when asked | The delivery session for this repository forbids opening PRs unless requested |
| Philippine business time and peso formatting | Kept (`Asia/Manila`, `₱`), configurable per organization | The legacy system's 12% VAT shows a Philippine context |

## Left out on purpose

| DriveOPX item | Reason |
| --- | --- |
| **Pre-beta credential posture** (agents may read, print, pass and commit credentials to the repository) | It is a decision by DriveOPX's owner for a private, pre-launch repository. It is unsafe as a default here: the retired desktop application committed a signing key (`ConstructOpxUI/*TemporaryKey.pfx`), a database file and commented-out connection strings with credentials. They left the tree when the app was deleted, but remain in git history `AGENTS.md` section 9 forbids committing credentials instead |
| Pre-beta renter history sharing | DriveOPX product decision, unrelated |
| DevFlow MCP checkpoints, Azure DevOps and Azure Boards workflow, `.mcp.json` | Tooling specific to the DriveOPX owner's accounts |
| Codex, Cursor, ZCode directories and model tables | Claude Code is the tool in use here; `AGENTS.md` stays tool-neutral |
| Android emulator and Maestro testing, Capacitor shell, staging VPS deployment, Staging database migration rules | No mobile shell or hosted staging exists for this repository yet. Add them when they do |
| Provider adapters (payments, social, ads, email, OCR) | No such integration exists in this product |
| Skills tied to DriveOPX workflows (`seed-data`, `release-mobile-apps`, `restore-hosted-staging`, user-story and board skills) | Not applicable |

## What the Drive handbooks added

The Drive folder holds DriveOPX's product, business and operations handbooks (about 2.7 MB of Markdown). They describe
the DriveOPX business, so only the engineering and operating practices were taken from them.

**Coverage, stated plainly.** Read in full: the Deployment Operations and Managed Migration Strategy, the Environment and
Cost Validation Strategy and the UI/UX audit report. Read in large part: the Staging Happy-Path Test Runbook (about two
thirds). Read selectively: the Infrastructure Operations Manual (front-door sections and about 20 dated entries) and the
Consolidated Handbook (the stack, delivery and baseline sections). Searched by keyword only: the Page-by-Page manual. Not
read: the 959 KB End-User Navigation map, and the product, market and payments parts of the Consolidated Handbook. The
same folder also holds credential and identity-document files; those were deliberately not opened.

**Adopted from them** (written up in `docs/operations/`):

| Practice | Where it lands here |
| --- | --- |
| `/health/live` (process only), `/health/ready` (database reachable and migrations current), `/health/version` (service, version, commit SHA, build time, environment) | `ConstructOPX.Api` health endpoints, `docs/operations/readiness-and-observability.md` |
| Errors return problem details with a trace id and never expose SQL, EF or stack text | `ApiExceptionHandler` |
| Immutable images tagged with the commit SHA; deploy by exact SHA; roll back to the previous image only when post-deploy health fails; an application rollback does not reverse a migration | `docs/operations/deployment.md`, `deploy/` |
| Migrations are additive first; keep the idempotent SQL script and run the "no pending model changes" check before applying | `docs/operations/database-and-migrations.md`, `.github/workflows/ci.yml` |
| Backups are `COPY_ONLY` with checksum and `RESTORE VERIFYONLY`; a backup is not a recovery point without a checksum, and not restore evidence without a test restore; alert when none exists for 26 hours; never restore over the live database to diagnose | `docs/operations/backup-and-restore.md`, `scripts/backup-database.sh` |
| Secrets live only in a password manager, protected CI secrets or root-only server files; prove existence without printing; keep separate application and admin database credentials | `docs/operations/secrets-and-access.md` |
| Tenant scope on every operation including background work; append-only audit trail; XSRF on writes; rate limiting on sign-in | `AGENTS.md` section 9, `ConstructOPX.Domain.Features.Audit` |
| Honest evidence vocabulary: never mark PASS from local evidence alone; a dependency that is not configured is BLOCKED, not a pass | `AGENTS.md` Delivery Evidence |
| UI findings that generalize: one scroller per dialog with focus restore, 44 px targets, no page-level horizontal overflow, disable an action only with a stated reason | `.agents/references/ui-ux-design-standards.md` |

**Rejected, or stronger here than in DriveOPX.** The handbooks admit that company and branch scope was not yet propagated
across every DriveOPX report, and that tenant checks are per-endpoint; here every query carries a tenant filter. They
describe the older stack with MediatR, Swashbuckle, Angular Material and Karma; none of that is used. They show a tracked
settings file holding credentials accepted as temporary risk; that is not accepted here. Retention jobs that hard-delete
expired audit or pay data are not adopted: this system has no purge job, and adding one needs an explicit retention and
legal-hold decision.
