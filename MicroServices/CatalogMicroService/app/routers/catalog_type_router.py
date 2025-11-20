import logging
from typing import List

from fastapi import APIRouter, Depends
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.dto.catalog_type_dto import CatalogTypeDTO
from app.repositories.catalog_type_repository import CatalogTypeRepository

router = APIRouter(prefix="/types", tags=["catalog-types"])

logger = logging.getLogger("catalog.router.types")

@router.get("", response_model=List[CatalogTypeDTO])
async def read_types(db: AsyncSession = Depends(get_db)):
    repo = CatalogTypeRepository(db)
    items = await repo.list_all()
    logger.info("Returning %s catalog types", len(items))
    return [CatalogTypeDTO.model_validate(item) for item in items]

@router.post("", response_model=CatalogTypeDTO)
async def add_type(type_dto: CatalogTypeDTO, db: AsyncSession = Depends(get_db)):
    logger.info("Creating catalog type '%s'", type_dto.type)
    repo = CatalogTypeRepository(db)
    catalog_type = type_dto.to_model()
    added_type = await repo.add(catalog_type)
    logger.info("Catalog type %s created", added_type.id)
    return CatalogTypeDTO.model_validate(added_type)