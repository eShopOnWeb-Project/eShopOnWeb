from fastapi import APIRouter, Depends, HTTPException
from fastapi.encoders import jsonable_encoder
from sqlalchemy.ext.asyncio import AsyncSession
from app import schemas, services, db

router = APIRouter(prefix="/api/v1/orders", tags=["orders"])

@router.post("", response_model=schemas.OrderRead, status_code=201)
async def create_order(order_in: schemas.OrderCreate, session: AsyncSession = Depends(db.get_session)):
    order = await services.create_order(session, order_in)
    return order

@router.get("/{order_id}", response_model=schemas.OrderRead)
async def get_order(order_id: int, session: AsyncSession = Depends(db.get_session)):
    order = await services.get_order(session, order_id)
    if not order:
        raise HTTPException(status_code=404, detail="Order not found")
    return order

@router.get("", response_model=list[schemas.OrderRead])
async def list_orders(buyer_id: str, session: AsyncSession = Depends(db.get_session)):
    orders = await services.list_orders_for_buyer(session, buyer_id) 
    return [schemas.OrderRead.from_orm(o) for o in orders] 
