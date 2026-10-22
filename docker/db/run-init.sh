#!/bin/bash
# Khởi tạo database: tạo DB nếu chưa có, rồi chạy lần lượt các script trong init/ (theo tên file).
# Mọi script đều chạy lại được nên mỗi lần deploy chạy lại cũng không ảnh hưởng dữ liệu.
set -euo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
[ -x "$SQLCMD" ] || SQLCMD=/opt/mssql-tools/bin/sqlcmd

DB="${DB_NAME:-SquishyPop}"
ARGS=(-S "${DB_HOST:-mssql}" -U sa -P "$MSSQL_SA_PASSWORD" -C -b)

echo "Đợi SQL Server sẵn sàng..."
for i in $(seq 1 90); do
    if "$SQLCMD" "${ARGS[@]}" -Q "SELECT 1" > /dev/null 2>&1; then break; fi
    if [ "$i" -eq 90 ]; then echo "Không kết nối được SQL Server" >&2; exit 1; fi
    sleep 2
done

"$SQLCMD" "${ARGS[@]}" -Q "IF DB_ID(N'$DB') IS NULL BEGIN CREATE DATABASE [$DB] COLLATE SQL_Latin1_General_CP1_CI_AS; PRINT N'Đã tạo database $DB'; END"

for f in /docker-db/init/*.sql; do
    echo "==> $(basename "$f")"
    "$SQLCMD" "${ARGS[@]}" -d "$DB" -i "$f"
done

echo "Database $DB đã sẵn sàng."
