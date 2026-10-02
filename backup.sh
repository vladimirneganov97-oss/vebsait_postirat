#!/usr/bin/env bash
set -euo pipefail

mkdir -p backups
STAMP=$(date +%Y-%m-%d_%H-%M-%S)

# SQLite is copied through the running container so the database stays inside the named volume.
docker compose exec -T app sh -c 'if command -v sqlite3 >/dev/null 2>&1; then sqlite3 /data/laundry.db ".backup /tmp/laundry-backup.db"; else cp /data/laundry.db /tmp/laundry-backup.db; fi'
docker cp laundry_app:/tmp/laundry-backup.db "backups/laundry_${STAMP}.db"
docker compose exec -T app rm -f /tmp/laundry-backup.db

echo "Backup created: backups/laundry_${STAMP}.db"
