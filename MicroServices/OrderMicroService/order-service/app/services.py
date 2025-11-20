from datetime import datetime, timezone
from decimal import Decimal
import asyncio
from typing import List

from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select

from app import models, schemas, events

import logging

logger = logging.getLogger(__name__)

# -----------------------
# Helper: calculate total
# -----------------------
def calculate_total(items: List[models.OrderItem]) -> float:
    return float(sum(item.unitprice * item.units for item in items))

# -----------------------
# Create a new order
# -----------------------
async def create_order(db: AsyncSession, order_in: schemas.OrderCreate) -> schemas.OrderRead:
    # Create Order model with timezone-aware UTC datetime
    order = models.Order(
        buyer_id=order_in.buyer_id,
        order_date=datetime.now(timezone.utc),
        shiptoaddress_street=order_in.shipping.street,
        shiptoaddress_city=order_in.shipping.city,
        shiptoaddress_state=order_in.shipping.state,
        shiptoaddress_country=order_in.shipping.country,
        shiptoaddress_zipcode=order_in.shipping.zip,
    )

    # Add items
    for it in order_in.items:
        order.items.append(
            models.OrderItem(
                itemordered_catalogitemid=it.itemordered_catalogitemid,
                itemordered_productname=it.itemordered_productname,
                itemordered_pictureuri=it.itemordered_pictureuri,
                unitprice=it.unitprice,
                units=it.units,
            )
        )

    db.add(order)
    try:
        await db.commit()
    except Exception:
        await db.rollback()
        logger.exception(
            "Database failure while creating order buyer_id=%s basket_id=%s",
            order_in.buyer_id,
            order_in.basket_id,
        )
        raise
    await db.refresh(order)

    logger.info(
        "Created order id=%s buyer_id=%s item_count=%s",
        order.id,
        order.buyer_id,
        len(order.items),
    )

    # Compute total
    total_amount = calculate_total(order.items)

    # Publish event to confirm stock
    confirm_items = [
        schemas.EventItem(
            itemId=it.itemordered_catalogitemid,
            amount=it.units,
            basketId=order_in.basket_id
        )
        for it in order.items
    ]
    confirm_payload = [item.model_dump() for item in confirm_items]
    asyncio.create_task(safe_publish("catalog_item_stock.confirm", confirm_payload))

    # Return OrderRead
    return schemas.OrderRead(
        id=order.id,
        buyer_id=order.buyer_id,
        order_date=order.order_date,  # use DB value
        shipping=schemas.Shipping(
            street=order.shiptoaddress_street,
            city=order.shiptoaddress_city,
            state=order.shiptoaddress_state,
            country=order.shiptoaddress_country,
            zip=order.shiptoaddress_zipcode,
        ),
        status=order.status,
        items=[
            schemas.OrderItemRead(
                id=i.id,
                itemordered_catalogitemid=i.itemordered_catalogitemid,
                itemordered_productname=i.itemordered_productname,
                itemordered_pictureuri=i.itemordered_pictureuri,
                unitprice=float(i.unitprice),
                units=i.units,
            )
            for i in order.items
        ],
        total=float(total_amount)
    )

async def safe_publish(routing_key, payload):
    try:
        await events.publisher.publish(routing_key, payload)
        payload_size = len(payload) if hasattr(payload, "__len__") else "unknown"
        logger.debug("Published event routing_key=%s payload_size=%s", routing_key, payload_size)
    except Exception:
        logger.exception("Failed to publish event '%s'", routing_key)

# -----------------------
# Get single order by ID
# -----------------------
async def get_order(db: AsyncSession, order_id: int) -> schemas.OrderRead | None:
    result = await db.execute(select(models.Order).where(models.Order.id == order_id))
    order = result.scalars().first()
    if not order:
        logger.debug("Order id=%s not found in database", order_id)
        return None

    total_amount = calculate_total(order.items)

    return schemas.OrderRead(
        id=order.id,
        buyer_id=order.buyer_id,
        order_date=order.order_date,  # use DB value
        shipping=schemas.Shipping(
            street=order.shiptoaddress_street,
            city=order.shiptoaddress_city,
            state=order.shiptoaddress_state,
            country=order.shiptoaddress_country,
            zip=order.shiptoaddress_zipcode,
        ),
        status=order.status,
        items=[
            schemas.OrderItemRead(
                id=i.id,
                itemordered_catalogitemid=i.itemordered_catalogitemid,
                itemordered_productname=i.itemordered_productname,
                itemordered_pictureuri=i.itemordered_pictureuri,
                unitprice=float(i.unitprice),
                units=i.units,
            )
            for i in order.items
        ],
        total=float(total_amount)
    )

# -----------------------
# List all orders for a buyer
# -----------------------
async def list_orders_for_buyer(db: AsyncSession, buyer_id: str) -> list[schemas.OrderRead]:
    result = await db.execute(
        select(models.Order)
        .where(models.Order.buyer_id == buyer_id)
        .order_by(models.Order.order_date.desc())
    )
    orders = result.scalars().all()
    logger.info("Fetched %s orders for buyer_id=%s", len(orders), buyer_id)

    return [
        schemas.OrderRead(
            id=o.id,
            buyer_id=o.buyer_id,
            order_date=o.order_date,  # use DB value
            shipping=schemas.Shipping(
                street=o.shiptoaddress_street,
                city=o.shiptoaddress_city,
                state=o.shiptoaddress_state,
                country=o.shiptoaddress_country,
                zip=o.shiptoaddress_zipcode,
            ),
            status=o.status,
            items=[
                schemas.OrderItemRead(
                    id=i.id,
                    itemordered_catalogitemid=i.itemordered_catalogitemid,
                    itemordered_productname=i.itemordered_productname,
                    itemordered_pictureuri=i.itemordered_pictureuri,
                    unitprice=float(i.unitprice),
                    units=i.units,
                )
                for i in o.items
            ],
            total=float(calculate_total(o.items))
        )
        for o in orders
    ]
