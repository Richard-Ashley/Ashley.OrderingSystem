# Ashley.OrderingSystem

> A .NET 10 microservices demo: three services communicating over HTTP and RabbitMQ events, with structured logs and correlation ids traceable across all of them in Seq.

[![CI](https://github.com/Richard-Ashley/Ashley.OrderingSystem/actions/workflows/ci.yml/badge.svg)](https://github.com/Richard-Ashley/Ashley.OrderingSystem/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)

A deliberately small order-placement system that exists to show how services talk to each other, not to model a business. Placing an order checks stock **synchronously** over HTTP, then announces the order **asynchronously** as an event that other services react to.

The point of the repo is the communication patterns, their failure modes, and being honest about both. [Known limitations](#known-limitations) is a first-class section.

---

## How it works

```
                 HTTP (synchronous)
┌──────────────┐  POST /api/inventory/check  ┌──────────────┐
│    Orders    │ ──────────────────────────▶ │  Inventory   │
│   :5101      │   "is there stock?"         │   :5102      │
└──────┬───────┘                             └──────┬───────┘
       │ publishes OrderPlaced                      │ publishes StockLow
       ▼                                            ▼
┌────────────────────────────────────────────────────────────┐
│                      RabbitMQ (events)                     │
└──────┬─────────────────────┬──────────────────────┬────────┘
       │ inventory-order-    │ notification-order-  │ orders-stock-low
       │ placed              │ placed               │ notification-stock-low
       ▼                     ▼                      ▼
   Inventory            Notification            Orders / Notification
   decrements stock     logs a "confirmation"   log a low-stock warning / alert
```

| Service | Port | Owns | Talks to |
|---|---|---|---|
| **OrdersService** | 5101 | Orders (SQLite) | Calls Inventory over HTTP; publishes `OrderPlaced`; consumes `StockLow` |
| **InventoryService** | 5102 | Products and stock (SQLite) | Answers stock checks; consumes `OrderPlaced`; publishes `StockLow` |
| **NotificationService** | 5103 | Nothing | Consumes `OrderPlaced` and `StockLow` and logs them (no real email) |

Each service has its own database and its own code. They share only two small libraries:

- `shared/Contracts`: the wire format (HTTP DTOs and event records) and nothing else.
- `shared/Observability`: cross-cutting infrastructure (Serilog setup, correlation-id middleware, HTTP handler and MassTransit filter).

### Events

| Event | Published by | Consumed by | Queue |
|---|---|---|---|
| `OrderPlaced` | Orders | Inventory | `inventory-order-placed` |
| `OrderPlaced` | Orders | Notification | `notification-order-placed` |
| `StockLow` | Inventory | Orders | `orders-stock-low` |
| `StockLow` | Inventory | Notification | `notification-stock-low` |

Each service prefixes its queue names, so two services consuming the same event each get their own copy. See [the queue naming note](#development-notes).

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/products/docker-desktop/) (RabbitMQ and Seq run in containers)

### One command

```bash
git clone https://github.com/Richard-Ashley/Ashley.OrderingSystem.git
cd Ashley.OrderingSystem

# Linux / macOS / WSL / Git Bash
./scripts/start-all.sh

# Windows PowerShell
.\scripts\start-all.ps1
```

The script starts RabbitMQ and Seq with Docker Compose, builds the solution, then starts the three services (Inventory and Notification first, so their queues exist before Orders publishes) with logs in `./logs`. Migrations and seed data are applied automatically on startup in Development.

Stop with `./scripts/stop-all.sh` or `.\scripts\stop-all.ps1`. Add `--infra` (or `-Infra`) to also stop RabbitMQ and Seq.

| What | URL |
|---|---|
| OrdersService Swagger | http://localhost:5101/swagger |
| InventoryService Swagger | http://localhost:5102/swagger |
| NotificationService Swagger | http://localhost:5103/swagger |
| RabbitMQ management | http://localhost:15672 (`ordering` / `ordering_dev_pass`) |
| Seq (logs) | http://localhost:8081 |

### Or manually

```bash
docker compose up -d rabbitmq seq
dotnet build
# one terminal each, in this order:
cd services/InventoryService    && dotnet run --no-build
cd services/NotificationService && dotnet run --no-build
cd services/OrdersService       && dotnet run --no-build
```

Always `dotnet build` before `dotnet run --no-build`. If migrations changed, a stale binary crashes at startup with a "pending model changes" error.

---

## Try it

Seed products (InventoryService):

| Id | Product | Stock |
|---|---|---|
| 1 | Wireless mouse | 50 |
| 2 | Mechanical keyboard | 25 |
| 3 | USB-C hub | 100 |
| 4 | 27-inch monitor | 15 |
| 5 | Desk lamp | 40 |
| 6 | Noise-canceling headset | 8 (already below the low-stock threshold of 10) |
| 7 | Limited edition mousepad | 0 (always out of stock) |
| 8 | Standing desk converter | 12 |
| 9 | Webcam cover slider | 200 |
| 10 | Laptop stand (aluminum) | 30 |

```bash
# Place an order: 201, then stock for product 1 drops from 50 to 48 a moment later
curl -X POST http://localhost:5101/api/orders -H "Content-Type: application/json" \
     -H "X-Correlation-Id: aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb" \
     -d '{"productId": 1, "quantity": 2, "customerEmail": "you@example.com"}'
curl http://localhost:5102/api/inventory/1

# Out of stock: 409
curl -X POST http://localhost:5101/api/orders -H "Content-Type: application/json" \
     -d '{"productId": 7, "quantity": 1, "customerEmail": "you@example.com"}'

# Invalid input: 400 with a field-by-field error dictionary
curl -X POST http://localhost:5101/api/orders -H "Content-Type: application/json" \
     -d '{"productId": 0, "quantity": -1, "customerEmail": "nope"}'

# Low stock: ordering product 6 publishes StockLow, which Orders and Notification both log
curl -X POST http://localhost:5101/api/orders -H "Content-Type: application/json" \
     -d '{"productId": 6, "quantity": 1, "customerEmail": "you@example.com"}'
```

Then open Seq at http://localhost:8081 and filter by `CorrelationId = 'aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb'` to see one order's whole journey across all three services: the HTTP calls, the event consumers and the notification.

Stop InventoryService and place an order to see the `503` after the HTTP timeout. Stop RabbitMQ and place an order to see the save-then-publish gap described below.

### Endpoints

| Service | Endpoint | Notes |
|---|---|---|
| Orders | `POST /api/orders` | 201 / 400 (validation) / 409 (no stock) / 503 (Inventory unreachable) |
| Orders | `GET /api/orders/{id}` | 200 / 404 |
| Inventory | `POST /api/inventory/check` | 200 / 400 / 404 (unknown product) |
| Inventory | `GET /api/inventory/{productId}`, `GET /api/inventory` | Browse stock |
| Notification | `GET /api/health` | Gives the event-only service something to probe |

---

## Technology

| Concern | Choice |
|---|---|
| Runtime | .NET 10, ASP.NET Core controllers |
| Persistence | EF Core 10 with SQLite, one database per service, migrations applied on startup in Development |
| Messaging | RabbitMQ 3.13 via **MassTransit 8.5.\*** (see [Development notes](#development-notes)) |
| Validation | FluentValidation 12 (core package, called explicitly) |
| Logging | Serilog: JSON on the console plus [Seq](https://datalust.co/seq) |
| API docs | Swashbuckle (Swagger UI) |
| Tests | xUnit, MassTransit test harness, FluentAssertions 7.2.2 |
| CI | GitHub Actions with RabbitMQ and Seq service containers |

---

## Design decisions

**HTTP for the stock check, events for everything after.** Placing an order needs an answer *now* (is there stock?), so that call is synchronous and blocking. What happens after an order exists (decrement stock, notify someone) does not need to hold up the caller, so it is an event any interested service can subscribe to.

**`POST` for the stock check.** The request has a body (`productId`, `quantity`) that fits the shared contract better than a query string, and the result changes over time, so there is nothing to cache. It is a deliberate bend of the usual "reads are GET" rule.

**Plain entities, not rich aggregates.** Orders and products are flat rows with public setters and a small state machine. The business rules live in the service layer. A [Clean Architecture version with real aggregates](https://github.com/Richard-Ashley/Ashley.CleanTemplate) is a separate repo; this one stays focused on communication.

**One product per order.** Line items would add domain modeling that has nothing to do with the point of the repo.

**Idempotent consumer.** RabbitMQ delivers at least once and MassTransit retries on failure, so `OrderPlaced` can arrive twice. InventoryService records processed `MessageId`s in a `ProcessedMessages` table, in the same transaction as the stock update, and skips repeats.

**Clamp, don't go negative.** If an order arrives for more than the remaining stock (see the race below), stock is clamped to zero and the overcommit is logged, so it is visible and not silent.

**Level-triggered `StockLow`.** Every decrement that leaves stock at or below the threshold (10) publishes the event. It is noisy, but deterministic and easy to demonstrate. Product 6 starts below the threshold, so it fires on the first order.

**A typed `HttpClient` with a visible timeout.** `Inventory:HttpTimeoutSeconds` (default 5) is in `appsettings.json`. A dead Inventory returns a clear `503`. Retries (Polly) are deliberately left out.

**Correlation ids end to end.** A request's `X-Correlation-Id` (or a generated GUID) is echoed on the response, forwarded on the HTTP call to Inventory, set as the MassTransit `CorrelationId` on published events, and added to every log line in the request or message scope. One id finds the whole flow in Seq.

---

## Testing and CI

```bash
dotnet test
```

15 unit tests cover InventoryService's `OrderPlacedConsumer` (run on the MassTransit in-memory test harness against in-memory SQLite) and the stock-check validator: first delivery, duplicate `MessageId`, clamping at zero, crossing and landing exactly on the threshold, level-triggered `StockLow`, and unknown products. During development I checked them by mutation: removing the idempotency check, removing the clamp, changing `<=` to `<` on the threshold, and disabling `StockLow` each make a test fail. That check was done by hand and is not automated.

The CI workflow, on every push and pull request to `main`:

1. Builds in Release and runs the unit tests (with coverage collected as an artifact).
2. Starts RabbitMQ and Seq as service containers, then the three services.
3. Places an order and asserts a `201`, the echoed `X-Correlation-Id` header, and that InventoryService's stock for the product drops (through the real event path).
4. Checks that events reached Seq.

What CI does **not** assert: correlation-id propagation across services (only the header echo), the `StockLow` flow end to end, or anything about broker outages. Those are checked by hand.

---

## Known limitations

These are deliberate scope boundaries, with the production fix noted for each.

- **Save-then-publish gap.** `OrderPlaced` is published after the order is saved, with no transactional outbox. If the process dies or the broker is unreachable between the two, the order exists and the event is lost, so stock is never decremented. Production fix: the transactional outbox (MassTransit has EF Core support).
- **Publish is time-boxed at 5 seconds** (`RabbitMq:PublishTimeoutSeconds`). With the broker down, the order is saved, the publish fails after 5 s and is logged, and the client still gets `201`. Without the time box the request hung for over a minute in testing.
- **Events can be lost for a while around a broker restart.** In one local test, publishes kept failing for roughly 35 seconds after the broker came back, until MassTransit's reconnect backoff re-established the connection. That is one observation, not a guarantee. An outbox would buffer these events.
- **Check-then-decrement race.** The stock check and the decrement are separate steps, so two concurrent orders can both pass the check for the last unit. The decrement clamps at zero and logs the overcommit. Production fix: stock reservations with a timeout.
- **Idempotency is keyed on `MessageId` only.** It covers broker redelivery. If the same logical order were republished with a fresh `MessageId` (for example by a misconfigured publisher retry), stock would be decremented twice. Production fix: also key on `OrderId`.
- **Level-triggered `StockLow` is noisy.** Every decrement on an already-low product publishes. Production fix: edge-trigger it, or debounce at the consumer.
- **Start order matters.** Inventory and Notification must start before Orders. A published event with no bound queue is dropped. Production fix: declare topology independently of consumer start-up.
- **Correlation ids are GUIDs only.** MassTransit's `CorrelationId` is a GUID, so an inbound non-GUID `X-Correlation-Id` is replaced with a fresh one.
- **Notification is a stub.** It logs instead of sending email.
- **No Dockerfiles.** The services run with `dotnet run`; Docker Compose runs only RabbitMQ and Seq. Containerizing them would be a natural next step.
- **Dev-only credentials.** `ordering_dev_pass` and Seq's unauthenticated first run (`SEQ_FIRSTRUN_NOAUTHENTICATION`) are in the repo for convenience. A real deployment would use secrets and put RabbitMQ and Seq behind a private network.
- **Seq is not open source.** It is free for individual developer use under its own license, which this repo accepts in `docker-compose.yml` (`ACCEPT_EULA`). It is one implementation of "structured logs plus a browsable UI". Grafana Loki would be an open-source alternative, and the services would not change beyond the sink.
- **Pending-model-changes crash.** Running `dotnet run --no-build` after migrations changed crashes at startup. Build first.

---

## Development notes

**MassTransit is pinned to 8.5.\*.** MassTransit 8.x is Apache-2.0, while the 9.x packages link to a separate license page (massient.com/license) in their NuGet metadata, which I understand to be commercial. For a public MIT repo I pinned the last Apache-2.0 line. Everything used here (publishing, consumers, the RabbitMQ transport, endpoint naming, the test harness) exists in 8.x. I did not read or evaluate the v9 license terms, so check them yourself before upgrading.

**Also pinned:** FluentAssertions 7.2.2, the last Apache-2.0 release.

**Queue naming.** MassTransit names a queue after its consumer type, which collides when two services consume the same event: they would share one queue and split the messages between them. Each service therefore prefixes its queue names (`inventory-…`, `notification-…`, `orders-…`). If you cloned this at or before commit `837606c` and ran it, RabbitMQ still holds the old unprefixed `OrderPlaced` and `StockLow` queues. Delete them in the management UI before starting, or they will accumulate copies of every event.

**Why a shared `Observability` project.** Three copies of the correlation middleware, handler and logging filter would drift. It holds only infrastructure; `Contracts` holds only wire types, so changing logging never changes a contract.

**Why the unit tests use SQLite.** The unit tests use in-memory SQLite instead of the EF InMemory provider because SQLite enforces keys and constraints like the real database.

---

## Project layout

```
Ashley.OrderingSystem/
├── services/
│   ├── OrdersService/          # orders, HTTP client to Inventory, OrderPlaced publisher, StockLow consumer
│   ├── InventoryService/       # stock, stock-check API, OrderPlaced consumer, StockLow publisher
│   └── NotificationService/    # OrderPlaced and StockLow consumers (log only)
├── shared/
│   ├── Contracts/              # HTTP DTOs and event records
│   └── Observability/          # Serilog, correlation id middleware, handler, MassTransit filter
├── tests/OrderingSystem.Tests/ # xUnit tests
├── scripts/                    # start-all / stop-all (.sh and .ps1)
└── docker-compose.yml          # RabbitMQ and Seq
```

---

## License

[MIT](LICENSE) © Richard Nii Asuku Ashley
