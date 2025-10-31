from pydantic import BaseModel, ConfigDict
from typing import List, Optional

class CatalogItemDTO(BaseModel):
    id: Optional[int] = None
    name: str
    description: str
    price: float
    picture_uri: str
    catalog_type_id: int
    catalog_brand_id: int

    model_config = ConfigDict(from_attributes=True)
    
    @classmethod
    def from_model(cls, item):
        return cls(
            id=item.id,
            name=item.name,
            description=item.description,
            price=item.price,
            picture_uri=item.picture_uri,
            catalog_type_id=item.catalog_type_id,
            catalog_brand_id=item.catalog_brand_id,
        )

    def to_model(self):
        # convert back to SQLAlchemy model
        from app.models.catalog_item import CatalogItem
        return CatalogItem(
            catalog_type_id=self.catalog_type_id,
            catalog_brand_id=self.catalog_brand_id,
            description=self.description,
            name=self.name,
            price=self.price,
            picture_uri=self.picture_uri
        )

