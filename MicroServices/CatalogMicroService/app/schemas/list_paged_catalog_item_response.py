from typing import List
from pydantic import BaseModel
from app.dto.catalog_item_dto import CatalogItemDTO


class ListPagedCatalogItemResponse(BaseModel):
    catalog_items: List[CatalogItemDTO] = []
    page_count: int = 0