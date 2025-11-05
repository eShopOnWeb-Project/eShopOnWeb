from pydantic import BaseModel, ConfigDict
from typing import List

class CatalogBrandDTO(BaseModel):
    id: int | None = None
    brand: str  

    model_config = ConfigDict(from_attributes=True)

    def to_model(self):
        from app.models.catalog_brand import CatalogBrand
        return CatalogBrand(brand=self.brand)