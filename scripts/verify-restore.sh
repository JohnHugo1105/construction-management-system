#!/usr/bin/env bash
# Proves a backup can be restored: restores it into a temporary database next to the real one, compares what it contains with the
# source database, then drops the temporary database. Never restores over a live database.
#
# Usage: scripts/verify-restore.sh [path-to-backup.bak]     (default: the newest backup of CMS_DB_NAME in CMS_BACKUP_DIR)
# Same environment as scripts/backup-database.sh.
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
run_sql() { "$SQLCMD" -S "$SERVER" -U "$LOGIN" "${TRUST[@]}" -b -h -1 -W -s "|" -Q "SET NOCOUNT ON; $1"; }
# Everything put inside a T-SQL string literal is either validated against a short allow-list or has its single quotes doubled.
sql_quote() { printf '%s' "${1//\'/\'\'}"; }

file="${1:-}"
if [ -n "$file" ] && { ! [[ "$file" =~ ^[A-Za-z0-9_./:\\\ -]+$ ]] || [[ "$file" == *..* ]]; }; then
  echo "The backup path may contain only letters, digits and _ . / : \\ - and spaces, and no '..'." >&2; exit 2
fi
if [ -z "$file" ]; then
  newest="$(ls -1t "$LOCAL_DIR"/"${DB}"_*.bak 2>/dev/null | head -n 1 || true)"
  [ -n "$newest" ] || { echo "No backup of $DB found in $LOCAL_DIR." >&2; exit 1; }
  file="$DIR/$(basename "$newest")"
  local_file="$newest"
else
  local_file="$LOCAL_DIR/$(basename "$file")"
fi

if [ -f "$local_file.sha256" ]; then
  (cd "$(dirname "$local_file")" && sha256sum -c "$(basename "$local_file").sha256") || { echo "The backup no longer matches its SHA-256. Do not use it." >&2; exit 1; }
fi

temp="${DB}_RestoreCheck_$(date -u +%Y%m%d%H%M%S)"
data_dir="$(run_sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(260));" | head -n 1)"
quoted_file="$(sql_quote "$file")"
echo "Restoring $(basename "$file") into temporary database [$temp]"

moves=""
while IFS='|' read -r logical _ physical_type _; do
  [ -z "$logical" ] && continue
  ext="mdf"; [ "$physical_type" = "L" ] && ext="ldf"
  # The logical name comes from inside the backup file, which is not trusted: quote it for T-SQL, and keep only safe characters in
  # the physical file name so it can never point outside the data folder.
  safe_name="${logical//[^A-Za-z0-9_]/_}"
  moves="$moves, MOVE N'$(sql_quote "$logical")' TO N'$(sql_quote "${data_dir}${temp}_${safe_name}.${ext}")'"
done < <(run_sql "RESTORE FILELISTONLY FROM DISK = N'${quoted_file}';")

cleanup() { run_sql "IF DB_ID(N'$temp') IS NOT NULL BEGIN ALTER DATABASE [$temp] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$temp]; END" >/dev/null 2>&1 || true; }
trap cleanup EXIT

run_sql "RESTORE DATABASE [$temp] FROM DISK = N'${quoted_file}' WITH CHECKSUM${moves};" >/dev/null

count() { run_sql "SELECT COUNT(*) FROM [$1].sys.tables;" | head -n 1; }
migrations() { run_sql "IF OBJECT_ID(N'[$1].dbo.__EFMigrationsHistory') IS NOT NULL EXEC(N'SELECT COUNT(*) FROM [$1].dbo.__EFMigrationsHistory') ELSE SELECT 0;" | head -n 1; }

restored_tables="$(count "$temp")"; restored_migrations="$(migrations "$temp")"
echo "Restored database: $restored_tables tables, $restored_migrations applied migrations."
if [ -n "$(run_sql "SELECT DB_ID(N'$DB');" | head -n 1 | tr -d ' ' | grep -v '^NULL$' || true)" ]; then
  source_tables="$(count "$DB")"; source_migrations="$(migrations "$DB")"
  echo "Source database:   $source_tables tables, $source_migrations applied migrations."
  if [ "$restored_tables" != "$source_tables" ] || [ "$restored_migrations" -gt "$source_migrations" ]; then
    echo "NOTE: the restored copy differs from the live database. That is expected if the schema changed after the backup was taken; check the date." >&2
  fi
fi
echo "OK: the backup restores. Temporary database [$temp] will now be dropped."
