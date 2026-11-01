# Secrets and access

## Where secrets live

* A password manager, protected CI environment secrets, or root-only files on the server. **Never** the repository, documents, chat,
  tickets, screenshots or logs. `.env` is git-ignored; `.env.example` lists names only.
* The application needs one secret: the database connection string (`CMS_DATABASE_CONNECTION`). Use a **dedicated application login**
  with rights only on the CMS database, separate from the administrator login used for migrations and backups.
* Backup storage uses its own credential that can write backups but cannot read the live data; the application credential cannot reach
  the backups. Keep evidence of both.

## Proving a secret exists without showing it

Record the secret's name, owner, rotation date and a fingerprint (for example the first 8 characters of its SHA-256), never the value.
Check files by owner, mode and size, not by printing them.

## If a credential is exposed

1. Restrict access first (disable the account or firewall the host).
2. Identify what it could reach and since when.
3. Rotate it.
4. Update the stores that hold it (password manager, CI secrets, server files).
5. Restart what uses it.
6. Confirm the old value no longer works.
7. Search the repository, logs and screenshots for other copies. Rotate anything that appeared in a screenshot.

**The retired desktop application's history contains secrets** (a signing key, a database file with accounts, and commented-out
database connection strings with credentials), reachable in git history at commit `93da838` and earlier. Treat them as exposed:
rotate any credential that was ever real, and do not reuse that signing key.

## People and sessions

* Roles are the access model (`docs/business-rules.md`, section 2). Give people the smallest set they need; review the user list monthly.
* Turning off an account ends its session within seconds. Administrators can reset a password (the person must change it at next
  sign-in) but never see a password; nobody can, because only salted hashes are stored.
* Sign-in is rate limited per address and locks an account for 15 minutes after five wrong passwords. The rate limit is per server
  process and resets on restart.

## Monthly review

Users and roles; accounts that no longer need access; secrets older than their rotation date; backup credential scope; dependency updates.
