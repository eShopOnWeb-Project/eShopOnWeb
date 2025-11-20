from typing import Optional

from pydantic import BaseModel, ConfigDict

from app.models.catalog_type import CatalogType

class CatalogTypeDTO(BaseModel):
    id: Optional[int] = None
    type: str

    model_config = ConfigDict(from_attributes=True)

    def to_model(self) -> CatalogType:
        return CatalogType(type=self.type)