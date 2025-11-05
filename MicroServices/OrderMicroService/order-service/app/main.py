from fastapi import FastAPI
from app.api.v1 import orders
import asyncio
from .logging_config import setup_logging
import logging
from fastapi import FastAPI

setup_logging()
logger = logging.getLogger(__name__)


app = FastAPI(title="Order Service")
app.include_router(orders.router)

from app.db import engine
from app import models

@app.on_event("startup")
async def startup():
    logger.info("Startup initiated")
    async with engine.begin() as conn:
        await conn.run_sync(models.Base.metadata.create_all)

if __name__ == "__main__":
    import uvicorn
    logger.info("Starting orders service...")
    uvicorn.run("app.main:app", host="0.0.0.0", port=8001, reload=True)
