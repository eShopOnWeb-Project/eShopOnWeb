import logging
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from app import schemas, services, db

router = APIRouter(prefix="/api/v1/orders", tags=["orders"])
logger = logging.getLogger(__name__)

@router.post("", response_model=schemas.OrderRead, status_code=201)
async def create_order(order_in: schemas.OrderCreate, session: AsyncSession = Depends(db.get_session)):
    logger.info("Received create_order request for buyer_id=%s basket_id=%s", order_in.buyer_id, order_in.basket_id)
    try:
        order = await services.create_order(session, order_in)
    except Exception as exc:
        logger.exception("Failed to create order for buyer_id=%s basket_id=%s", order_in.buyer_id, order_in.basket_id)
        raise HTTPException(status_code=500, detail="Unable to create order") from exc
    return order

@router.get("/{order_id}", response_model=schemas.OrderRead)
async def get_order(order_id: int, session: AsyncSession = Depends(db.get_session)):
    logger.debug("Fetching order_id=%s", order_id)
    try:
        order = await services.get_order(session, order_id)
    except Exception as exc:
        logger.exception("Failed to load order_id=%s", order_id)
        raise HTTPException(status_code=500, detail="Unable to fetch order") from exc
    if not order:
        logger.info("Order not found order_id=%s", order_id)
        raise HTTPException(status_code=404, detail="Order not found")
    return order

@router.get("", response_model=list[schemas.OrderRead])
async def list_orders(buyer_id: str, session: AsyncSession = Depends(db.get_session)):
    logger.debug("Listing orders for buyer_id=%s", buyer_id)
    try:
        orders = await services.list_orders_for_buyer(session, buyer_id)
    except Exception as exc:
        logger.exception("Failed to list orders for buyer_id=%s", buyer_id)
        raise HTTPException(status_code=500, detail="Unable to list orders") from exc
    return orders
