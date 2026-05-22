#!/bin/sh
set -eu

PORT="${PORT:-10000}"
export GUNICORN_BIND="${GUNICORN_BIND:-127.0.0.1:8001}"

sed "s/__PORT__/${PORT}/g" /app/nginx/nginx.conf > /tmp/nginx.conf

echo "Starting Gunicorn on ${GUNICORN_BIND} (not exposed publicly)..."
gunicorn -c /app/gunicorn.conf.py run:app &
GUNICORN_PID=$!

cleanup() {
  kill "$GUNICORN_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "Starting nginx edge on port ${PORT}..."
exec nginx -c /tmp/nginx.conf -g 'daemon off;'
