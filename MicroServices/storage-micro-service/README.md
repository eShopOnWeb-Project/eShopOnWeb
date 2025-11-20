# Storage Microservice

Microservice responsible for maintaining catalog item stock levels for **eShopOnWeb**.  
Built with [NestJS 11](https://docs.nestjs.com/), it persists data through **PostgreSQL / TypeORM** and communicates with the rest of the platform by emitting and consuming events over **RabbitMQ**.

---

## Features

- **Atomic stock mutations**: restock, reserve, confirm, cancel, and validation-only flows.
- **Reservation lifecycle support** with per-basket pessimistic locking and expiry handling.
- **RabbitMQ integrations** for both RPC (request/response) and pub/sub patterns.
- **Cron job** that releases expired reservations and broadcasts the result.
- **Structured logging** with correlation IDs and rich context for easier observability.
- **Comprehensive Jest unit tests** covering operational happy-paths and common error cases.

---

## Architecture Overview

| Component | Description |
| --- | --- |
| `CatalogItemStockService` | Business logic for stock adjustments, reservation bookkeeping, validations, and event publishing. |
| `CatalogItemStockConsumer` | RabbitMQ handlers (RPC + subscribers) delegating to the service. |
| `ReservationCleanupCronService` | Scheduled task that cancels expired reservations and restores stock. |
| Entities (`CatalogItemStock`, `Reservation`) | TypeORM models backed by PostgreSQL tables. |
| DTOs (`DefaultDTOItem`, `FullDTOItem`) | Transport payloads exchanged via RabbitMQ. |

### Message Contracts

All messages share the `DefaultDTOItem` shape:

```ts
{
  itemId: number;   // required
  amount: number;   // required
  basketId?: number // required for reserve/confirm/cancel flows, optional for restock/check
}
```

| Operation | Transport | Routing key / Queue | Notes |
| --- | --- | --- | --- |
| Restock | Subscribe | `catalog_item_stock.restock` | `basketId` optional; publishes `catalog_item_stock.restock.success`. |
| Reserve | RPC | `catalog_item_stock.reserve` (`…_reserve_rpc_queue`) | Creates/updates reservations, publishes `reserve.success`. |
| Confirm | Subscribe | `catalog_item_stock.confirm` | Moves reserved → sold, emits `confirm.success`. |
| Cancel | Subscribe | `catalog_item_stock.cancel` | Releases reserved units, emits `cancel.success`. |
| Get Full Stock | RPC | `catalog_item_stock.getall` (`…_getall_queue`) | Returns `FullDTOItem[]`. |
| Check Active Reservations | RPC | `catalog_item_stock.check_active_reservations` | Verifies reservation coverage for a basket. |

> Events are emitted on `catalog_item_stock.exchange`. See `src/stock/catalog-item-stock.consumer.ts` for queue bindings.

---

## Getting Started

### Prerequisites

| Tool | Version (tested) |
| --- | --- |
| Node.js | 22.x (LTS) |
| npm | 10.x |
| PostgreSQL | 14+ |
| RabbitMQ | 3.12+ (with default vhost) |

Make sure RabbitMQ and PostgreSQL are reachable from the service.  
Default connection values are overridable via environment variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `DATABASE_HOST` | `storage-db` | Postgres host |
| `DATABASE_PORT` | `5432` | Postgres port |
| `DATABASE_USER` | `postgres` | Postgres user |
| `DATABASE_PASSWORD` | `postgres` | Postgres password |
| `DATABASE_NAME` | `storagedb` | Postgres database |
| `RABBITMQ_URI` | `amqp://guest:guest@rabbitmq:5672` | RabbitMQ connection string |

### Install

```bash
npm ci
```

### Run (development)

```bash
npm run start:dev
```

This starts:
1. HTTP server on port `3000` (used for health checks / future extensions).
2. RabbitMQ microservice connected to `catalog_item_stock.exchange`.

### Run (production)

```bash
npm run build
npm run start:prod
```

Ensure the `dist/` folder is built and that all env vars are set in the target environment.

---

## Testing

```bash
npm test          # Jest unit tests
npm run test:cov  # Coverage
npm run lint      # ESLint + Prettier
```

The primary spec file is `src/__tests__/catalog-item-stock.service.spec.ts`, which covers:
- Stock fetching, restock, reserve, confirm, cancel flows.
- Error handling (insufficient stock, reservation mismatches, invalid inputs).
- Reservation validations (per basket, expiration checks).

---

## Logging & Observability

- Uses NestJS `Logger` with structured context for every operation.
- Correlation IDs (e.g., `reserve-<timestamp>-<random>`) added at the consumer level for RPC/event tracing.
- Custom error classes (`src/stock/errors/stock-errors.ts`) emit error codes such as `INVALID_INPUT`, `INSUFFICIENT_STOCK`, `RESERVATION_NOT_FOUND`, helping downstream services respond consistently.

Example warning emitted when a confirm payload is malformed:

```
[CatalogItemStockConsumer] Confirm batch failed [confirm-…]: Invalid basketId: undefined for itemId 1
```

---

## Project Structure

```
src/
├─ main.ts                           # Bootstrap HTTP + RabbitMQ endpoints
├─ app.module.ts                     # Root Nest module
├─ stock/
│  ├─ catalog-item-stock.service.ts  # Core business logic
│  ├─ catalog-item-stock.consumer.ts # RabbitMQ handlers
│  ├─ cron/reservation-cleanup-…     # Expiration cron job
│  ├─ dto/                           # DTO contracts
│  ├─ entities/                      # TypeORM entities
│  └─ errors/                        # Custom error types
└─ __tests__/                        # Jest unit tests
```

---

## Troubleshooting

| Symptom | Likely Cause | Resolution |
| --- | --- | --- |
| `Invalid basketId` warnings | Missing/zero basketId on reserve/confirm/cancel events | Ensure upstream services include the basketId used during reservation. |
| `Insufficient stock` errors during reserve | Requested quantity > available | Retry later or restock. Message includes available amount for diagnostics. |
| `EventPublishError` | RabbitMQ unreachable | Verify `RABBITMQ_URI` and broker availability. |
| `DatabaseOperationError` | Postgres connectivity issues or migrations missing | Check database credentials, ensure tables exist (auto-sync enabled in dev). |

---

## Contributing

1. Fork / branch from `main`.
2. Add tests for the scenario you’re improving.
3. Run `npm test && npm run lint`.
4. Submit a PR describing the change, expected behaviour, and any config updates.

---

## License

This project is currently marked **UNLICENSED**. Consult the maintainers before reusing the code outside the eShopOnWeb ecosystem.
