#!/usr/bin/env bash
# Stops the three services started by start-all.sh. Pass --infra to also stop RabbitMQ and Seq.
set -uo pipefail
cd "$(dirname "$0")/.."

if [ -f logs/pids ]; then
  while read -r pid; do
    [ -n "$pid" ] && kill "$pid" 2>/dev/null || true
  done < logs/pids
  rm -f logs/pids
  echo "Services stopped."
else
  echo "No logs/pids file; nothing to stop."
fi

if [ "${1:-}" = "--infra" ]; then
  docker compose stop rabbitmq seq
fi
