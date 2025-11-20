# events.py
import logging
import os
import json
from aio_pika import connect_robust, Message, ExchangeType
from typing import List
from app.schemas import EventItem

RABBIT_URL = os.getenv("RABBITMQ_URL", "amqp://guest:guest@rabbitmq/")
EXCHANGE_NAME = "catalog_item_stock.exchange"
logger = logging.getLogger(__name__)

class RabbitMQPublisher:
    def __init__(self):
        self.connection = None
        self.channel = None
        self.exchange = None

    async def connect(self):
        if self.connection and not self.connection.is_closed:
            return
        try:
            self.connection = await connect_robust(RABBIT_URL)
            self.channel = await self.connection.channel()
            self.exchange = await self.channel.declare_exchange(
                EXCHANGE_NAME, ExchangeType.TOPIC, durable=True
            )
            logger.info("Connected to RabbitMQ exchange '%s'", EXCHANGE_NAME)
        except Exception:
            logger.exception("Failed to connect to RabbitMQ at %s", RABBIT_URL)
            raise

    async def publish(self, routing_key: str, payload: dict):
        if self.connection is None or self.connection.is_closed:
            await self.connect()
        try:
            body = json.dumps(payload).encode()
            message = Message(body, content_type="application/json", delivery_mode=2)
            await self.exchange.publish(message, routing_key)
            logger.debug("Published message to routing_key=%s", routing_key)
        except Exception:
            logger.exception("Failed to publish message to routing_key=%s", routing_key)
            raise

    async def close(self):
        if self.connection and not self.connection.is_closed:
            try:
                await self.connection.close()
                logger.info("Closed RabbitMQ connection")
            except Exception:
                logger.exception("Failed to close RabbitMQ connection")
                raise

publisher = RabbitMQPublisher()
