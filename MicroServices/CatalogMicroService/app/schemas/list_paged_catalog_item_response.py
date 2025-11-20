from typing import List

from pydantic import BaseModel, Field

from app.dto.catalog_item_dto import CatalogItemDTO


class ListPagedCatalogItemResponse(BaseModel):
    catalog_items: List[CatalogItemDTO] = Field(default_factory=list)
    page_count: int = 0