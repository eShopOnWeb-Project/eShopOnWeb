#!/bin/sh
set -e

echo "Waiting for Postgres..."

until pg_isready -h catalog-db -p 5432 -U cataloguser; do
  echo "Postgres not ready, sleeping..."
  sleep 2
done

echo "Postgres ready, running migrations..."
alembic upgrade head

echo "Starting API..."
exec uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload