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
* Turning off an account, resetting or changing its password, and signing out all end the person's sessions on every device within
  seconds: each session cookie carries a security stamp that changes at those moments, and a copied cookie with the old stamp is refused.
  Administrators can reset a password (the person must change it at next sign-in) but never see a password; nobody can, because only
  salted hashes are stored.
* Sign-in and change-password are rate limited per address (10 a minute by default, `RateLimiting__SignInPerMinute`) and an account is
  locked for 15 minutes after five wrong passwords. The rate limit is per server process and resets on restart. The address is the
  person's real address because the web server (nginx) takes it from the proxy in front of it, and only from private networks; see
  `deploy/web/nginx.conf` and add the proxy's address there if it is public. The API must be reachable only through nginx.
* The session keys (`DataProtection__KeysPath`) are secrets too: they decrypt every session cookie. Keep that folder private, persistent
  and backed up with the database; losing it only signs everyone out, but leaking it lets someone forge a session cookie.
* The API connects as the `cms_app` login (read and write the CMS tables, nothing else), created by `deploy/sql/create-app-login.sql`.
  The administrator login is used only for migrations, backups and that script. Rotate the application password by running the script
  again with a new `CMS_APP_DB_PASSWORD` and restarting the API.

## Monthly review

Users and roles; accounts that no longer need access; secrets older than their rotation date; backup credential scope; dependency updates.
