# Backup and restore

A backup is a **recovery point** only when it has a checksum and has been **copied off the server**. It is **restore evidence** only
when it has actually been restored. Do both on a schedule.

## Taking a backup

```bash
export CONSTRUCTOPX_DB_PASSWORD='...'           # from the password manager; never typed into a command line
scripts/backup-database.sh            # database ConstructOpx on localhost,1433 by default; see the script header for every setting
```

What it does, each step checked:
1. `BACKUP DATABASE ... WITH COPY_ONLY, CHECKSUM, COMPRESSION`: copy-only so it never disturbs another backup job's chain;
   checksum so page corruption is caught while writing.
2. `RESTORE VERIFYONLY ... WITH CHECKSUM`: re-reads the file.
3. Writes `<file>.bak.sha256`, so any copy can later be proven identical.

With the compose files the backup folder is `./backups` on the host (mounted into the SQL container at `/var/opt/mssql/backup`). Set
`CONSTRUCTOPX_BACKUP_DIR=/var/opt/mssql/backup` (as SQL Server sees it) and `CONSTRUCTOPX_BACKUP_LOCAL_DIR=./backups` (as the script sees it).
The script needs `sqlcmd` (the `mssql-tools18` package, or run it inside the SQL container).

The session keys (the `api-keys` volume, or wherever `DataProtection__KeysPath` points) are not in the database. Losing them only signs
everyone out, so they need no special care beyond being kept private; back them up with the rest if you want sessions to survive a
rebuilt server.

**Document files.** Project documents uploaded as files are kept on disk, not in the database: the `api-files` volume, or wherever
`FileStorage__RootPath` points. The database holds each file's name, size, SHA-256 and its place in the folder, so the two must be backed up
**together**, on the same schedule, and restored together. Back up the files folder right after the database backup (for example
`docker run --rm -v constructopx_api-files:/files:ro -v "$PWD/backups:/out" alpine tar czf /out/files-$(date -u +%Y%m%dT%H%M%SZ).tar.gz -C /files .`
and a `sha256sum` of the archive). Files are only ever added, never changed, so a file backup taken a little after the database backup
holds every file that backup refers to. A document whose file is missing after a restore downloads as "missing from file storage" and
the rest of the system keeps working; restore the files archive to bring it back.

**Schedule:** nightly. Copy each file and its `.sha256` to storage on **another machine** (object storage or a second server) with a
credential that can write backups but cannot read or delete the live data. Keep 14 days at least, longer for month ends.

## Proving a backup restores

```bash
scripts/verify-restore.sh                     # newest backup, or pass a path
```

It verifies the SHA-256 (refusing a file that changed), restores into a **temporary database** next to the real one, compares table
and migration counts with the source, then drops the temporary database. It never restores over a live database. Run it **weekly**
and keep the output as evidence.

Verified on 1 Nov 2026 against SQL Server 2022 (16.0.4305): backup, verify, checksum, restore to a temporary database, comparison and
cleanup all succeeded, and a copy with one byte changed was refused by the checksum check (exit 1).

## Alerts to set up

* No successful backup in 26 hours.
* A verify-restore failure, or a SHA-256 mismatch.
* Backup storage nearly full.

Alerts without a named owner do not count: write down who receives them.

## Restoring for real (disaster or mistake)

1. Stop the API (`docker compose stop api web`) so nothing writes.
2. **Take a fresh backup of what is there now**, even if it is damaged: you may need it.
3. Verify the SHA-256 of the backup you will use, then `RESTORE VERIFYONLY ... WITH CHECKSUM`.
4. Restore into a **new database name** first (and the document files archive into a **new folder** next to the live one) (`ConstructOpx_Restored`), inspect it (`select count(*)` on `Projects`, `CostEntries`; the latest
   `__EFMigrationsHistory` row), and only then decide: either point the API at the new database (change the connection string) or,
   if you must replace the live one, rename the damaged database first (`ALTER DATABASE ... MODIFY NAME`) instead of overwriting it.
5. Start the API and check `GET /health/ready` and `GET /health/version`.
6. Write down what was restored, from which file, and the data lost since (everything after the backup time).

Never restore over the live database to find out what is in a backup. Never run a restore script or SQL file you did not write or
read. The retired desktop app restored by running a whole file as SQL after dropping the table; there is no such feature here by design.

## What a backup does not contain

The API keeps two kinds of state outside the database: the session keys (see above) and the **document files** folder
(`FileStorage__RootPath`). Back up the database and the document files together, with the secrets in the password manager. Recovery-time and recovery-point targets are not claimed until a
rehearsal on a replacement server has been done and timed.
