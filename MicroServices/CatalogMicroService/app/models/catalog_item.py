from sqlmodel import Field, Relationship, SQLModel
from sqlalchemy import DECIMAL, Column
from typing import Optional
from datetime import datetime
from app.models.catalog_type import CatalogType
from app.models.catalog_brand import CatalogBrand


class CatalogItem(SQLModel, table=True):
    id: int = Field(default=None, primary_key=True)

    name: str = Field(max_length=50, nullable=False)
    description: Optional[str] = None
    price: float = Field(sa_column=Column(DECIMAL(18, 2), nullable=False))
    picture_uri: Optional[str] = None

    catalog_type_id: int = Field(foreign_key="catalogtype.id")
    catalog_brand_id: int = Field(foreign_key="catalogbrand.id")

    catalog_type: Optional[CatalogType] = Relationship(back_populates="items")
    catalog_brand: Optional[CatalogBrand] = Relationship(back_populates="items")

    def update_details(self, name: str, description: str, price: float):
        if not name or not description or price <= 0:
            raise ValueError("Invalid details")
        self.name = name
        self.description = description
        self.price = price

    def update_brand(self, catalog_brand_id: int):
        if catalog_brand_id <= 0:
            raise ValueError("Invalid brand ID")
        self.catalog_brand_id = catalog_brand_id

    def update_type(self, catalog_type_id: int):
        if catalog_type_id <= 0:
            raise ValueError("Invalid type ID")
        self.catalog_type_id = catalog_type_id

    def update_picture_uri(self, picture_name: Optional[str]):
        if not picture_name:
            self.picture_uri = ""
            return
        self.picture_uri = f"images/products/{picture_name}?{datetime.now().timestamp()}"
