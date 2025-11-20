# eShopOnWeb – Distributed Commerce Playground

This repository extends the original Microsoft eShopOnWeb sample into a
polyglot, microservice-heavy lab. It keeps the ASP.NET Core MVC storefront,
adds a Blazor admin app, and layers in Go, Python, .NET, and NestJS services
behind an mTLS-enabled API gateway with shared monitoring and credentials.

## Project at a Glance

- `eShopOnWeb-main/` – original monolith solution (`src/Web`, `ApplicationCore`,
  `Infrastructure`, `BlazorAdmin`, tests, and shared assets).
- `MicroServices/` – standalone bounded contexts (Catalog, Orders, Basket,
  Storage, API Gateway) with their own runtimes, databases, and Dockerfiles.
- `Monitoring/` – Loki + Promtail + Grafana stack for logs and dashboards.
- `infra/` – Bicep templates and parameters for deploying the platform to Azure.
- Root-level Compose files (`all-services.yml`, `compose.common.yml`,
  `docker-compose-adminPages.yml`) wire everything together plus shared RabbitMQ,
  pgAdmin, secrets, and networks.

## Architecture Overview

- **Clients**: MVC storefront (port `5106` when containerized) and Blazor Admin.
- **API Gateway (Go)**: Terminates JWT-authenticated requests, enforces mTLS to
  downstream services, load-balances `/catalog` and `/orders`, and exposes `:8090`.
- **Domain Services**:
  - Catalog (FastAPI + Postgres, async SQLAlchemy) with Alembic migrations.
  - Order (FastAPI) persisting to Postgres and publishing events through RabbitMQ.
  - Basket (.NET 8 Web API) + Postgres for user carts.
  - Storage (NestJS + TypeORM) for inventory stock with scheduled workers.
- **Cross-cutting**: RabbitMQ for messaging, pgAdmin for DB introspection,
  Loki/Grafana for logs, Docker secrets for TLS & JWTs, Azure SQL Edge for the
  legacy MVC app, and optional Infrastructure-as-Code for cloud rollout.

## Microservices & Apps

| Component | Tech | Responsibilities |
|-----------|------|------------------|
| `src/Web` | ASP.NET Core MVC | Customer-facing storefront, talks to SQL Edge and RabbitMQ. |
| `BlazorAdmin` | Blazor WebAssembly + ASP.NET Core | Admin UI calling the gateway to orchestrate microservices. |
| `MicroServices/ApiGatewayMicroService` | Go | Reverse proxy, JWT validation/issuing, client cert auth toward services. |
| `MicroServices/CatalogMicroService` | FastAPI, SQLAlchemy, Postgres | Product catalog CRUD, seeded data, async endpoints. |
| `MicroServices/OrderMicroService/order-service` | FastAPI, RabbitMQ, Postgres | Order placement, status tracking, event publication. |
| `MicroServices/BasketService` | ASP.NET Core Minimal API | Basket CRUD + Postgres persistence, secured by gateway JWTs. |
| `MicroServices/storage-micro-service` | NestJS, TypeORM, Postgres | Stock reservation & background jobs integrating with RabbitMQ. |

Each service ships with its own `docker-compose.yml`, `Dockerfile`, migrations,
and health checks so you can run them in isolation or under the umbrella stack.

## Monitoring & Observability

- `Monitoring/docker-compose-grafana.yml` launches Grafana (`localhost:3001`),
  Loki (`3100`), and Promtail for container log shipping.
- Dashboards persist via the `grafana-data` volume; Loki indices live in
  `loki-data`. Import community dashboards or define alerts straight from Grafana.
- When `all-services.yml` is used, the monitoring stack boots automatically so
  every container’s stdout/err is queryable through Loki (`{job="docker"}`).

## Secret Management & Certificates

- `compose.common.yml` defines a `certgen` init container that runs
  `gen-secrets.sh` once and stores output in the `eshop-dev-secrets` Docker
  volume.
- The script provisions:
  - A development root CA (`/secrets/ca`).
  - Per-service TLS certs (matching service DNS names in Compose).
  - Client certificates for the gateway (`/secrets/clients/gateway`).
  - HMAC/JWT material (`/secrets/jwt`) used both by the gateway (private) and
    downstream services (public).
- Services mount the secrets volume read-only and point `TLS_CERT`, `TLS_KEY`,
  `MTLS_CLIENT_*`, or `JWT_*` variables to those files, giving you mTLS and JWT
  validation without checking keys into git.

## Docker & Compose Workflows

1. Ensure Docker Engine/Desktop with Compose v2 is installed.
2. Create the shared network once (Compose files mark it as external):
   ```powershell
   docker network create eshop-on-web-net
   ```
3. From the repo root, start the full stack:
   ```powershell
   docker compose -f all-services.yml up --build
   ```
   `all-services.yml` `include`s:
   - `compose.common.yml` (cert generation & secrets volume)
   - `docker-compose-adminPages.yml` (RabbitMQ management UI + pgAdmin)
   - `Monitoring/docker-compose-grafana.yml`
   - `eShopOnWeb-main/eShopOnWeb-main/docker-compose.yml` (MVC app + SQL Edge)
   - Every microservice compose file under `MicroServices/`
4. Use `docker compose -f all-services.yml down` (add `-v` to clear volumes) to
   stop everything. Individual services can be iterated in their directories
   with `docker compose up --build {service}` if you only need one context.

### Useful Ports

- Storefront MVC: `http://localhost:5106`
- API Gateway: `http://localhost:8090` (JWT required)
- RabbitMQ UI: `http://localhost:15672` (`guest` / `guest`)
- pgAdmin: `http://localhost:8080`
- Grafana: `http://localhost:3001` (`admin` / `admin`)
- Catalog API: internal `https://catalog:8000`
- Order API: internal `https://orders:8001`

## Infrastructure as Code

- `infra/main.bicep` plus `*.bicep` modules under `infra/core` describe Azure
  resources (App Service, databases, networking, security).
- `azure.yaml` includes deployment metadata, and `main.parameters.json` carries
  sample parameter values. Use `az deployment sub create` or `az deployment group`
  workflows to provision cloud environments mirroring the local topology.

## Getting Productive

- **Configuration**: App settings live under each project (`appsettings.*.json`,
  `.env`, etc.). Override via environment variables when running containers.
- **Data**: Each service seeds its own database (SQL Edge, Postgres) on startup.
  Connect with pgAdmin/SQL tools if you need manual inspection.
- **Testing**: Unit tests are under `tests/` and service-specific folders
  (PyTest, Jest, xUnit). Run them from the respective project roots.
- **Extensibility**: Add new services by copying one of the existing Compose
  setups, adding it to `all-services.yml`, and pointing the gateway or MVC app
  to the new endpoints.

This README should now serve as a high-level guide to the moving parts,
how they cooperate, and where to tweak them.
