#!/usr/bin/env bash
set -euo pipefail

if [ ! -f .env ]; then
  echo "ERROR: .env not found. Copy .env.example to .env and edit it first."
  exit 1
fi

if grep -q 'CHANGE_THIS_TO_A_LONG_RANDOM_PASSWORD' .env; then
  echo "ERROR: Change LAUNDRY_ADMIN_PASSWORD in .env before deployment."
  exit 1
fi

if grep -q '^DOMAIN=laundry.example.com' .env; then
  echo "ERROR: Change DOMAIN in .env to your real domain."
  exit 1
fi

echo "Building and starting the laundry site..."
docker compose up -d --build

echo
echo "Deployment started. Check status with: docker compose ps"
echo "View logs with: docker compose logs -f app"
