from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from app.database import get_db
from app.repositories.catalog_item_repository import CatalogItemRepository
from app.dto.catalog_item_dto import CatalogItemDTO
from app.schemas.delete_catalog_item_response import DeleteCatalogItemResponse
from app.schemas.list_paged_catalog_item_response import ListPagedCatalogItemResponse
from typing import Optional

router = APIRouter(prefix="/items", tags=["catalog-items"])
base_url="http://localhost:8000"  

# GET /items/{id}
@router.get("{catalog_item_id}", response_model=CatalogItemDTO)
async def get_catalog_item(catalog_item_id: int, db: AsyncSession = Depends(get_db)):
    repo = CatalogItemRepository(db)
    item = await repo.get_by_id(catalog_item_id)
    if not item:
        raise HTTPException(status_code=404, detail="Catalog item not found")
    dto = CatalogItemDTO.model_validate(item)
    return dto

@router.get("", response_model=ListPagedCatalogItemResponse)
async def list_catalog_items(
    pageSize: Optional[int] = None,
    pageIndex: int = 0,
    catalogBrandId: Optional[int] = None,
    catalogTypeId: Optional[int] = None,
    db: AsyncSession = Depends(get_db)
):
    repo = CatalogItemRepository(db)

    # count total items
    total_items = await repo.count_catalog_items(db, catalogBrandId, catalogTypeId)

    if pageSize is None:
        # fetch all items when pageSize is not specified
        items = await repo.list_catalog_items(
            db,
            skip=0,
            take=total_items,
            brand_id=catalogBrandId,
            type_id=catalogTypeId
        )
        page_count = 1 if total_items > 0 else 0
    else:
        # fetch paginated items
        items = await repo.list_catalog_items(
            db,
            skip=pageIndex * pageSize,
            take=pageSize,
            brand_id=catalogBrandId,
            type_id=catalogTypeId
        )
        page_count = (total_items + pageSize - 1) // pageSize

    # map to DTOs
    catalog_items = [CatalogItemDTO.model_validate(i) for i in items]

    return ListPagedCatalogItemResponse(
        catalog_items=catalog_items,
        page_count=page_count
    )

# POST /items
@router.post("", response_model=CatalogItemDTO)
async def create_catalog_item(item: CatalogItemDTO, db: AsyncSession = Depends(get_db)):
    repo = CatalogItemRepository(db)
    catalog_item = item.to_model()
    new_item = await repo.add(catalog_item)
    dto = CatalogItemDTO.model_validate(new_item)
    return dto

# PUT /items
@router.put("", response_model=CatalogItemDTO)
async def update_catalog_item(item: CatalogItemDTO, db: AsyncSession = Depends(get_db)):
    repo = CatalogItemRepository(db)
    existing = await repo.get_by_id(item.id)
    if not existing:
        raise HTTPException(status_code=404, detail="Catalog item not found")
    existing.name = item.name
    existing.description = item.description
    existing.price = item.price
    existing.catalog_brand_id = item.catalog_brand_id
    existing.catalog_type_id = item.catalog_type_id
    updated_item = await repo.update(existing)
    dto = CatalogItemDTO.model_validate(updated_item)
    return dto

# DELETE /items/{id}
@router.delete("/{catalog_item_id}", response_model=DeleteCatalogItemResponse)
async def delete_catalog_item(catalog_item_id: int, db: AsyncSession = Depends(get_db)):
    repo = CatalogItemRepository(db)
    existing = await repo.get_by_id(catalog_item_id)
    if not existing:
        raise HTTPException(status_code=404, detail="Catalog item not found")
    await repo.delete(existing)
    return DeleteCatalogItemResponse()