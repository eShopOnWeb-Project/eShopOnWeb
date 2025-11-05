from fastapi import APIRouter, Depends
from sqlalchemy.ext.asyncio import AsyncSession
from app.database import get_db
from app.repositories.catalog_brand_repository import CatalogBrandRepository
from app.dto.catalog_brand_dto import CatalogBrandDTO
from typing import List

router = APIRouter(prefix="/brands", tags=["catalog-brands"])

@router.get("", response_model=List[CatalogBrandDTO])
async def read_brands(db: AsyncSession = Depends(get_db)):
    repo = CatalogBrandRepository(db)
    items = await repo.list_all()
    return [CatalogBrandDTO.model_validate(item) for item in items]