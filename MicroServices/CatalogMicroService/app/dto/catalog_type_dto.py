from pydantic import BaseModel, ConfigDict
from typing import List, Optional
from app.models.catalog_type import CatalogType

class CatalogTypeDTO(BaseModel):
    id: Optional[int] = None
    type: str

    model_config = ConfigDict(from_attributes=True)

    def to_model(self) -> CatalogType:
        return CatalogType(type=self.type)