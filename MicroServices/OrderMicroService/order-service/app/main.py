from fastapi import FastAPI
from app.api.v1 import orders
import asyncio

app = FastAPI(title="Order Service")
app.include_router(orders.router)

from app.db import engine
from app import models

@app.on_event("startup")
async def startup():
    async with engine.begin() as conn:
        await conn.run_sync(models.Base.metadata.create_all)

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app.main:app", host="0.0.0.0", port=8001, reload=True)
