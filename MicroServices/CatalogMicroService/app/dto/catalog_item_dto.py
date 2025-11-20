from typing import Optional

from pydantic import BaseModel, ConfigDict

class CatalogItemDTO(BaseModel):
    id: Optional[int] = None
    name: str
    description: str
    price: float
    picture_uri: str
    catalog_type_id: int
    catalog_brand_id: int

    model_config = ConfigDict(from_attributes=True)
    
    def to_model(self):
        from app.models.catalog_item import CatalogItem
        return CatalogItem(
            catalog_type_id=self.catalog_type_id,
            catalog_brand_id=self.catalog_brand_id,
            description=self.description,
            name=self.name,
            price=self.price,
            picture_uri=self.picture_uri
        )

