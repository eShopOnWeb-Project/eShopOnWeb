# events.py
import os
import json
from aio_pika import connect_robust, Message, ExchangeType
from typing import List
from app.schemas import EventItem

RABBIT_URL = os.getenv("RABBITMQ_URL", "amqp://guest:guest@rabbitmq/")
EXCHANGE_NAME = "catalog_item_stock.exchange"

class RabbitMQPublisher:
    def __init__(self):
        self.connection = None
        self.channel = None
        self.exchange = None

    async def connect(self):
        if self.connection and not self.connection.is_closed:
            return
        self.connection = await connect_robust(RABBIT_URL)
        self.channel = await self.connection.channel()
        self.exchange = await self.channel.declare_exchange(
            EXCHANGE_NAME, ExchangeType.TOPIC, durable=True
        )

    async def publish(self, routing_key: str, payload: dict):
        if self.connection is None or self.connection.is_closed:
            await self.connect()
        body = json.dumps(payload).encode()
        message = Message(body, content_type="application/json", delivery_mode=2)
        await self.exchange.publish(message, routing_key)

    async def close(self):
        if self.connection and not self.connection.is_closed:
            await self.connection.close()

publisher = RabbitMQPublisher()
