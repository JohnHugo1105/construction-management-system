# Database and migrations

SQL Server 2022 with EF Core 10. The schema changes only through **migrations** (one model, one history in
`__EFMigrationsHistory`). Production never creates or alters tables any other way.

## Adding a migration (developers)

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Cms.Infrastructure --startup-project src/Cms.Api --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/Cms.Infrastructure --startup-project src/Cms.Api   # must say none
```

Rules:
* **Additive first.** Add the column or table in one release; make code depend on it in the next; remove old things last. A new
  required column gets a default or is filled in the same migration.
* Review the generated `Up` **and** `Down`. A `Down` that would lose data must refuse to run once dependent data exists.
* If upstream added a migration while you worked, delete your unapplied migration and add it again so its timestamp and the model
  snapshot follow upstream's.
* CI fails when the model has changes without a migration, and applies all migrations to an empty database in the integration tests.

## Applying a migration to a real database

Production applies a **reviewed script**, not `Database:MigrateOnStartup` (which exists for development and tests only):

```bash
dotnet ef migrations script --idempotent --project src/Cms.Infrastructure --startup-project src/Cms.Api -o artifacts/migrate-<release>.sql
```

1. **Review the script** (the diff of the schema, any data statements) and attach it to the release record.
2. **Take a verified backup** (`scripts/backup-database.sh`) and confirm the restore check passed recently.
3. Apply the script with `sqlcmd -I -b -i artifacts/migrate-<release>.sql -d <database>` in a window, with the API stopped or the old
   API version known to be compatible with the new schema (additive migrations are). **`-I` is required**: the schema has filtered
   unique indexes, and without it `sqlcmd` fails with "CREATE INDEX failed because the following SET options have incorrect settings:
   'QUOTED_IDENTIFIER'". The script is idempotent: applying it again changes nothing. (Verified on 1 Nov 2026 after the last model change: a fresh SQL Server 2022
   database got 36 tables plus the migration history, and a second run was a no-op.)
4. Start the new API; `GET /health/ready` reports `migrations: current` only when none is pending.
5. Record: release id, script name, who applied it, when, the backup it follows.

An application rollback (running the previous image) **does not reverse a migration**. Prefer rolling forward with a corrective
migration; restore the pre-migration backup only as a last resort, accepting the loss of data written since.

## Data fixes

No ad-hoc `UPDATE` or `DELETE` against production. A correction is a feature (void, reverse, archive) or a reviewed migration with a
backup taken first. Financial records are voided with a reason, never edited in place or deleted. Retention purges do not exist; adding
one needs an explicit retention and legal-hold decision.

## Local development database

`docker compose -f deploy/docker-compose.yml up -d sql`, then run the API in Development: until the first migration exists it creates
the schema from the model; afterwards it applies migrations at start (`Database:MigrateOnStartup`). Reset by dropping the database;
never reset a shared or production database.
