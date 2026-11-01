#!/usr/bin/env bash
# Takes a verified backup of the CMS database.
#
#   * COPY_ONLY, so it never disturbs the differential/log backup chain of any other backup job.
#   * CHECKSUM, so page corruption is detected while writing; then RESTORE VERIFYONLY re-reads the file.
#   * A SHA-256 sidecar file, so a copy of the backup can be proven identical later.
#
# A backup without a checksum is not a recovery point, and a backup that was never restored is not restore evidence:
# run scripts/verify-restore.sh on a schedule too (docs/operations/backup-and-restore.md).
#
# Configuration (environment; the password is never accepted as an argument and never printed):
#   SQLCMDPASSWORD or CMS_DB_PASSWORD   the SQL login's password (required)
#   CMS_SQL_SERVER        default localhost,1433
#   CMS_SQL_USER          default sa (use a dedicated backup login in production)
#   CMS_SQL_TRUST_CERT    default true (set false when the server has a certificate the client trusts)
#   CMS_DB_NAME           default Cms
#   CMS_BACKUP_DIR        directory as SQL Server sees it, default /var/opt/mssql/backup
#   CMS_BACKUP_LOCAL_DIR  the same directory as this script sees it (differs when SQL Server runs in a container
#                         with a mounted volume); default CMS_BACKUP_DIR
set -euo pipefail

export SQLCMDPASSWORD="${SQLCMDPASSWORD:-${CMS_DB_PASSWORD:-}}"
: "${SQLCMDPASSWORD:?Set SQLCMDPASSWORD or CMS_DB_PASSWORD}"
SERVER="${CMS_SQL_SERVER:-localhost,1433}"
LOGIN="${CMS_SQL_USER:-sa}"
DB="${CMS_DB_NAME:-Cms}"
DIR="${CMS_BACKUP_DIR:-/var/opt/mssql/backup}"
LOCAL_DIR="${CMS_BACKUP_LOCAL_DIR:-$DIR}"
TRUST=()
[ "${CMS_SQL_TRUST_CERT:-true}" = "true" ] && TRUST=(-C)

if ! [[ "$DB" =~ ^[A-Za-z0-9_]+$ ]]; then echo "CMS_DB_NAME may contain only letters, digits and underscores." >&2; exit 2; fi
SQLCMD="$(command -v sqlcmd || echo /opt/mssql-tools18/bin/sqlcmd)"
[ -x "$SQLCMD" ] || { echo "sqlcmd was not found. Install mssql-tools18." >&2; exit 2; }

run_sql() { "$SQLCMD" -S "$SERVER" -U "$LOGIN" "${TRUST[@]}" -b -h -1 -W -Q "$1"; }

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
file="$DIR/${DB}_${stamp}.bak"
local_file="$LOCAL_DIR/${DB}_${stamp}.bak"

echo "Backing up [$DB] to $file"
run_sql "SET NOCOUNT ON; BACKUP DATABASE [$DB] TO DISK = N'$file' WITH COPY_ONLY, CHECKSUM, COMPRESSION, STATS = 25;"

echo "Verifying the backup file (RESTORE VERIFYONLY ... WITH CHECKSUM)"
run_sql "SET NOCOUNT ON; RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM;"

if [ -r "$local_file" ]; then
  (cd "$LOCAL_DIR" && sha256sum "$(basename "$local_file")" > "$(basename "$local_file").sha256")
  echo "SHA-256 recorded in $(basename "$local_file").sha256"
else
  echo "Note: $local_file is not readable from here, so no SHA-256 sidecar was written. Run sha256sum where the file lives." >&2
fi

echo "OK: $(basename "$file") is a verified backup. It is a recovery point only once copied off this server and restore-tested."
