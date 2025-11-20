## Catalog Microservice

FastAPI-based catalog service used by the eShopOnWeb microservices sample. It exposes CRUD APIs for catalog items, brands, and types, and automatically seeds the backing database on startup for local development.

### Tech Stack
- FastAPI & Pydantic for the HTTP layer
- SQLModel/SQLAlchemy (async) with Alembic migrations
- PostgreSQL (production) or SQLite (tests/local)
- Uvicorn ASGI server

### Project Layout
- `app/main.py` – FastAPI app, lifespan hooks, router registration
- `app/server.py` – CLI entry point for Uvicorn with optional mTLS
- `app/database.py` – Async engine/session management + bootstrap logic
- `app/repositories/` – Data access layer per aggregate
- `app/routers/` – HTTP endpoints grouped by resource
- `app/schemas/` & `app/dto/` – Pydantic response/request models
- `app/seeder.py` – Deterministic seed data for dev/test environments
- `tests/` – Pytest suite covering repositories

### Prerequisites
- Python 3.12+
- Access to a PostgreSQL database (or SQLite URI) for `DATABASE_URL`
- OpenSSL certs if you plan to run with mTLS (optional)

### Getting Started
1. **Clone & enter the repo**
   ```powershell
   git clone <repo-url>
   cd MicroServices/CatalogMicroService
   ```
2. **Create & activate a virtual environment**
   ```powershell
   python -m venv venv
   .\venv\Scripts\activate
   ```
3. **Install dependencies**
   ```powershell
   pip install -r requirements.txt
   ```
4. **Configure environment variables**
   - Copy `.env.example` to `.env` (or create `.env`) with at least:
     ```
     DATABASE_URL=postgresql+asyncpg://user:password@localhost:5432/catalog
     LOG_LEVEL=INFO
     ```
   - For SQLite you can set `DATABASE_URL=sqlite+aiosqlite:///./catalog.db`
5. **Initialize & run**
   ```powershell
   uvicorn app.main:app --reload
   ```
   or use the bundled launcher:
   ```powershell
   python app/server.py
   ```

On startup the lifespan hook recreates schema objects and runs the seeders (`app/seeder.py`), ensuring predictable data for local testing.

### Running Tests
1. Activate the virtual environment (`.\venv\Scripts\activate`).
2. Ensure the repository is on your `PYTHONPATH` so tests can import the `app` package:
   ```powershell
   $env:PYTHONPATH = (Get-Location)
   ```
3. Execute pytest:
   ```powershell
   pytest
   ```

### Logging & Error Handling
- Global logging is configured via `LOG_LEVEL` (default `INFO`), producing structured lines like `timestamp logger [LEVEL] message`.
- Centralized error handlers translate domain exceptions (`ServiceError`, `DatabaseOperationError`, etc.) into JSON responses while logging stack traces for operators.

### Environment Variables
| Variable | Description | Default |
| --- | --- | --- |
| `DATABASE_URL` | Async SQLAlchemy URL. Required. | _None_ |
| `LOG_LEVEL` | Root log level (`DEBUG`, `INFO`, …). | `INFO` |
| `API_PORT` | Port when launching via `app/server.py`. | `8000` |
| `UVICORN_LOG_LEVEL` | Log level for Uvicorn access logs. | `info` |
| `TLS_CERT`, `TLS_KEY`, `TLS_CA` | When all set, the service enforces mutual TLS. | _unused_ |

### Key API Routes
- `GET /items/{id}` – Fetch a catalog item
- `GET /items` – Filtered & paginated list
- `POST /items` – Create item
- `PUT /items` – Update item
- `DELETE /items/{id}` – Delete item
- `GET /brands` – List catalog brands
- `GET /types` / `POST /types` – Manage catalog types

Use the built-in FastAPI docs at `http://localhost:8000/docs` for interactive exploration once the service is running.

### Troubleshooting
- **`ModuleNotFoundError: No module named 'app'`** – Set `PYTHONPATH` to the repo root (see “Running Tests”).
- **Database seeding errors** – Ensure `DATABASE_URL` is reachable and the user has schema modification rights.
- **TLS startup warnings** – If `TLS_CERT/TLS_KEY/TLS_CA` are not set, the server logs a warning and continues without mTLS; set all three to enable it.

### Contributing
1. Fork & clone the repo
2. Create a feature branch
3. Run `pytest` before submitting PRs
4. Describe changes clearly and include screenshots/logs when relevant

### License
This microservice inherits the licensing terms of the broader eShopOnWeb project. Refer to the root repository’s license for details.

