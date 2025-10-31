from sqlalchemy import Column, Integer, String, DateTime, Numeric, ForeignKey
from sqlalchemy.orm import relationship, declarative_base
import datetime

Base = declarative_base()

def utcnow():
    return datetime.datetime.now(datetime.timezone.utc)

class Order(Base):
    __tablename__ = "orders"
    id = Column(Integer, primary_key=True, index=True)
    buyer_id = Column(String(256), nullable=False, index=True)
    order_date = Column(DateTime(timezone=True), default=utcnow)

    shiptoaddress_street = Column(String(180), nullable=True)
    shiptoaddress_city = Column(String(100), nullable=True)
    shiptoaddress_state = Column(String(60), nullable=True)
    shiptoaddress_country = Column(String(90), nullable=True)
    shiptoaddress_zipcode = Column(String(18), nullable=True)

    status = Column(String(50), nullable=False, default='PENDING')

    items = relationship(
        "OrderItem",
        back_populates="order",
        cascade="all, delete-orphan",
        lazy="selectin"
    )


class OrderItem(Base):
    __tablename__ = "orderitems"
    id = Column(Integer, primary_key=True)
    order_id = Column(Integer, ForeignKey("orders.id", ondelete="CASCADE"))

    itemordered_catalogitemid = Column(Integer, nullable=True)
    itemordered_productname = Column(String(50), nullable=True)
    itemordered_pictureuri = Column(String(500), nullable=True)

    unitprice = Column(Numeric(18,2), nullable=False)
    units = Column(Integer, nullable=False)

    order = relationship("Order", back_populates="items")
