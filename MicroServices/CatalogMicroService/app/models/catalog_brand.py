from sqlmodel import Field, Relationship, SQLModel
from typing import List, TYPE_CHECKING
from sqlalchemy import Column, String

if TYPE_CHECKING:
    from app.models.catalog_item import CatalogItem 

class CatalogBrand(SQLModel, table=True):
    id: int = Field(default=None, primary_key=True)
    brand: str = Field(sa_column=Column(String(100), nullable=False))

    items: List["CatalogItem"] = Relationship(back_populates="catalog_brand")