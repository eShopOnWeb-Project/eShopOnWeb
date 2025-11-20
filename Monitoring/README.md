# Monitoring Stack

This folder contains a lightweight observability stack built with Docker
Compose: Loki for log storage, Promtail for log shipping, and Grafana for
visualization. Follow the steps below to run the stack and build log-focused
dashboards.

## Prerequisites

- Docker Desktop (or Docker Engine) with Compose v2
- Ports `3001` (Grafana) and `3100` (Loki) available on the host

## Running the Stack

```bash
cd Monitoring
docker compose -f docker-compose-grafana.yml up -d
```

Services:

- `grafana` at http://localhost:3001 (default credentials `admin` / `admin`)
- `loki` at http://localhost:3100
- `promtail` tails Docker container logs and pushes them to Loki

Dashboards, users, and plugins persist via the named volume `grafana-data`. Loki
indexes live in `loki-data`. Back up these volumes if you need to migrate or
rebuild the environment.

## Accessing Grafana

1. Open http://localhost:3001 and sign in with `admin` / `admin`.
2. Change the password when prompted (recommended).

## Adding Loki as a Data Source

1. In Grafana, go to `Connections → Add new data source`.
2. Select `Loki`.
3. Set the URL to `http://loki:3100`.
4. Click `Save & Test` to confirm Grafana can reach Loki.

## Exploring Logs (LogQL)

1. Navigate to `Explore → Loki`.
2. Use the label browser to pick labels Promtail adds (e.g., `job="docker"` or
   `container="eshopwebapp"`).
3. Run a query such as `{job="docker"}` to stream raw logs.
4. Filter with expressions like `{job="docker"} |= "error"` or aggregate counts:
   `count_over_time({container="eshopwebapp"}[1m])`.

## Building a Dashboard

1. `Dashboards → New → New dashboard → Add visualization`.
2. Pick `Loki` as the data source.
3. Choose a panel type:
   - `Logs` for raw log lines.
   - `Time series` to graph counts, e.g.
     `sum by (container)(count_over_time({job="docker"}[5m]))`.
4. Customize panel options (legend, labels, thresholds).
5. Save the panel, repeat for additional panels (errors, latency buckets, etc.).
6. Click `Save dashboard`, name it, and optionally add tags.

## Optional Enhancements

- Add `restart: unless-stopped` to services for resilience.
- Move credentials to environment files or Docker secrets.
- Extend Promtail config to parse log levels or ship host syslogs.
- Import community Loki dashboards (Grafana.com → Search for “Loki logs” →
  copy dashboard ID → `Dashboards → Import`).
- Configure alert rules from a dashboard panel (`Alert → Create alert rule`) to
  notify on error spikes or missing logs.

## Tear Down

```bash
docker compose -f docker-compose-grafana.yml down
```

Add `-v` to remove persistent data if you want a clean slate:

```bash
docker compose -f docker-compose-grafana.yml down -v
```

Let the volume backups and Grafana exports live under version control if you
need to reproduce dashboards elsewhere.

