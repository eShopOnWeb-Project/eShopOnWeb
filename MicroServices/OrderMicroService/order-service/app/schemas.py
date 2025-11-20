from datetime import datetime
from pydantic import BaseModel, Field
from typing import List, Optional

# ----------------------- Input Schemas -----------------------
class OrderItemCreate(BaseModel):
    itemordered_catalogitemid: Optional[int]
    itemordered_productname: Optional[str]
    itemordered_pictureuri: Optional[str]
    unitprice: float
    units: int

class Shipping(BaseModel):
    street: str
    city: str
    state: str
    country: str
    zip: str
    
    class Config:
        from_attributes = True

class OrderCreate(BaseModel):
    buyer_id: str
    basket_id: int
    shipping: Shipping
    items: List[OrderItemCreate]

# ----------------------- Output Schemas -----------------------
class OrderItemRead(BaseModel):
    id: int
    itemordered_catalogitemid: Optional[int]
    itemordered_productname: Optional[str]
    itemordered_pictureuri: Optional[str]
    unitprice: float  # <-- change to float
    units: int



class OrderRead(BaseModel):
    id: int
    buyer_id: str
    order_date: datetime
    shipping: Shipping
    status: str
    items: List[OrderItemRead]
    total: float

    class Config:
        from_attributes = True


# ----------------------- Event Schemas -----------------------

class EventItem(BaseModel):
    itemId: int
    amount: int
    basketId: int