import os
import json
import asyncio
from aio_pika import connect_robust, Message, ExchangeType

RABBIT_URL = os.getenv("RABBITMQ_URL", "amqp://guest:guest@rabbitmq/")

async def publish_event(routing_key: str, payload: dict):
    connection = await connect_robust(RABBIT_URL)
    async with connection:
        channel = await connection.channel()
        exchange = await channel.declare_exchange(
            "orders", ExchangeType.TOPIC, durable=True
        )
        message = Message(
            json.dumps(payload).encode(), content_type="application/json"
        )
        await exchange.publish(message, routing_key)
