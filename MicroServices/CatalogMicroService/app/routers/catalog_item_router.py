import logging
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.dto.catalog_item_dto import CatalogItemDTO
from app.repositories.catalog_item_repository import CatalogItemRepository
from app.schemas.delete_catalog_item_response import DeleteCatalogItemResponse
from app.schemas.list_paged_catalog_item_response import ListPagedCatalogItemResponse

router = APIRouter(prefix="/items", tags=["catalog-items"])

logger = logging.getLogger("catalog.router.items")

@router.get("/{catalog_item_id}", response_model=CatalogItemDTO)
async def get_catalog_item(catalog_item_id: int, db: AsyncSession = Depends(get_db)):
    logger.info("Fetching catalog item %s", catalog_item_id)
    repo = CatalogItemRepository(db)
    item = await repo.get_by_id(catalog_item_id)
    if not item:
        logger.warning("Catalog item %s not found", catalog_item_id)
        raise HTTPException(status_code=404, detail="Catalog item not found")
    dto = CatalogItemDTO.model_validate(item)
    logger.debug("Catalog item %s returned", catalog_item_id)
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
    logger.info(
        "Listing catalog items page_size=%s page_index=%s brand_id=%s type_id=%s",
        pageSize,
        pageIndex,
        catalogBrandId,
        catalogTypeId,
    )

    total_items = await repo.count_catalog_items(catalogBrandId, catalogTypeId)

    if pageSize is None:
        items = await repo.list_catalog_items(
            skip=0,
            take=total_items,
            brand_id=catalogBrandId,
            type_id=catalogTypeId
        )
        page_count = 1 if total_items > 0 else 0
    else:
        items = await repo.list_catalog_items(
            skip=pageIndex * pageSize,
            take=pageSize,
            brand_id=catalogBrandId,
            type_id=catalogTypeId
        )
        page_count = (total_items + pageSize - 1) // pageSize

    catalog_items = [CatalogItemDTO.model_validate(i) for i in items]
    logger.info(
        "Returning %s catalog items (page_count=%s) from %s total",
        len(catalog_items),
        page_count,
        total_items,
    )

    return ListPagedCatalogItemResponse(
        catalog_items=catalog_items,
        page_count=page_count
    )

@router.post("", response_model=CatalogItemDTO)
async def create_catalog_item(item: CatalogItemDTO, db: AsyncSession = Depends(get_db)):
    logger.info("Creating catalog item with name '%s'", item.name)
    repo = CatalogItemRepository(db)
    catalog_item = item.to_model()
    new_item = await repo.add(catalog_item)
    dto = CatalogItemDTO.model_validate(new_item)
    logger.info("Catalog item %s created", dto.id)
    return dto

@router.put("", response_model=CatalogItemDTO)
async def update_catalog_item(item: CatalogItemDTO, db: AsyncSession = Depends(get_db)):
    logger.info("Updating catalog item %s", item.id)
    repo = CatalogItemRepository(db)
    existing = await repo.get_by_id(item.id)
    if not existing:
        logger.warning("Catalog item %s not found for update", item.id)
        raise HTTPException(status_code=404, detail="Catalog item not found")
    existing.name = item.name
    existing.description = item.description
    existing.price = item.price
    existing.catalog_brand_id = item.catalog_brand_id
    existing.catalog_type_id = item.catalog_type_id
    updated_item = await repo.update(existing)
    dto = CatalogItemDTO.model_validate(updated_item)
    return dto

@router.delete("/{catalog_item_id}", response_model=DeleteCatalogItemResponse)
async def delete_catalog_item(catalog_item_id: int, db: AsyncSession = Depends(get_db)):
    logger.info("Deleting catalog item %s", catalog_item_id)
    repo = CatalogItemRepository(db)
    existing = await repo.get_by_id(catalog_item_id)
    if not existing:
        logger.warning("Catalog item %s not found for deletion", catalog_item_id)
        raise HTTPException(status_code=404, detail="Catalog item not found")
    await repo.delete(existing)
    logger.info("Catalog item %s deleted", catalog_item_id)
    return DeleteCatalogItemResponse()