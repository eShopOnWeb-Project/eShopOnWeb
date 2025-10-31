from fastapi import APIRouter, Depends
from sqlalchemy.ext.asyncio import AsyncSession
from app.database import get_db
from app.repositories.catalog_type_repository import CatalogTypeRepository
from app.dto.catalog_type_dto import CatalogTypeDTO
from typing import List

router = APIRouter(prefix="/types", tags=["catalog-types"])

@router.get("", response_model=List[CatalogTypeDTO])
async def read_types(db: AsyncSession = Depends(get_db)):
    repo = CatalogTypeRepository(db)
    items = await repo.list_all()
    return [CatalogTypeDTO.model_validate(item) for item in items]

@router.post("", response_model=CatalogTypeDTO)
async def add_type(type_dto: CatalogTypeDTO, db: AsyncSession = Depends(get_db)):
    repo = CatalogTypeRepository(db)
    catalog_type = type_dto.to_model()  # convert DTO -> SQLModel
    added_type = await repo.add(catalog_type)
    return CatalogTypeDTO.model_validate(added_type)  # convert back to DTO