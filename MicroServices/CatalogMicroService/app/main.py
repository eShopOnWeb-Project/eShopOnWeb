import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core.error_handlers import register_exception_handlers
from app.core.logging import configure_logging
from app.database import init_db
from app.routers.catalog_brand_router import router as catalog_brand_router
from app.routers.catalog_item_router import router as catalog_item_router
from app.routers.catalog_type_router import router as catalog_type_router

configure_logging()
logger = logging.getLogger("catalog.app")

@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("Starting application lifespan; initializing database")
    await init_db()
    logger.info("Database ready")
    yield
    logger.info("Application shutdown complete")

app = FastAPI(title="Catalog Microservice", lifespan=lifespan)
register_exception_handlers(app)

# app.add_middleware(
#     CORSMiddleware,
#     allow_origins=["*"],
#     allow_credentials=False,
#     allow_methods=["*"],
#     allow_headers=["*"],
# )

app.include_router(catalog_item_router)
app.include_router(catalog_brand_router)
app.include_router(catalog_type_router)