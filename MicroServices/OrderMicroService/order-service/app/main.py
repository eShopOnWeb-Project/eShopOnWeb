from fastapi import FastAPI
from app.api.v1 import orders
from app import events
from app.db import engine
from app import models
from .logging_config import setup_logging
import logging

setup_logging()
logger = logging.getLogger(__name__)

app = FastAPI(title="Order Service")
app.include_router(orders.router)

@app.on_event("startup")
async def startup():
    logger.info("Starting up: connecting RabbitMQ and initializing DB")
    await events.publisher.connect()
    async with engine.begin() as conn:
        await conn.run_sync(models.Base.metadata.create_all)

@app.on_event("shutdown")
async def shutdown():
    logger.info("Shutting down: closing RabbitMQ connection")
    await events.publisher.close()

if __name__ == "__main__":
    import uvicorn
    logger.info("Starting orders service...")
    uvicorn.run("app.main:app", host="0.0.0.0", port=8001, reload=True)
