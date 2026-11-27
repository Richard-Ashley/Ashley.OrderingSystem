#!/usr/bin/env bash
# Starts RabbitMQ and Seq (Docker), builds the solution, and runs the three services in the
# background. Logs go to ./logs, process ids to ./logs/pids. Stop everything with stop-all.sh.
set -euo pipefail
cd "$(dirname "$0")/.."

mkdir -p logs
if [ -s logs/pids ]; then
  echo "Services already started (logs/pids exists). Run scripts/stop-all.sh first." >&2
  exit 1
fi

echo "Starting RabbitMQ and Seq..."
docker compose up -d rabbitmq seq
until [ "$(docker inspect -f '{{.State.Health.Status}}' ordering-rabbitmq 2>/dev/null)" = "healthy" ]; do
  sleep 2
done

# Build first: running a stale binary after a migration change crashes at startup.
echo "Building..."
dotnet build --nologo -v quiet

start_service() { # name, port
  local name="$1" port="$2"
  (
    cd "services/$name"
    ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:$port" \
      dotnet "bin/Debug/net10.0/$name.dll" > "../../logs/$name.log" 2>&1 &
    echo $! >> ../../logs/pids
  )
  for _ in $(seq 1 60); do
    curl -sf "http://localhost:$port/swagger/v1/swagger.json" > /dev/null && { echo "  $name ready on :$port"; return 0; }
    sleep 1
  done
  echo "  $name did not start; see logs/$name.log" >&2
  return 1
}

# Inventory and Notification first so their queues exist before Orders publishes.
start_service InventoryService 5102
start_service NotificationService 5103
start_service OrdersService 5101

cat <<'MSG'

Everything is running.
  OrdersService        http://localhost:5101/swagger
  InventoryService     http://localhost:5102/swagger
  NotificationService  http://localhost:5103/swagger
  RabbitMQ management  http://localhost:15672   (ordering / ordering_dev_pass)
  Seq (logs)           http://localhost:8081

Try it:
  curl -X POST http://localhost:5101/api/orders -H "Content-Type: application/json" \
       -d '{"productId": 1, "quantity": 2, "customerEmail": "you@example.com"}'
Stop with scripts/stop-all.sh
MSG
