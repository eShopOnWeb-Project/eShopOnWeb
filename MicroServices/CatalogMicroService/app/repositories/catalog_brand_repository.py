import logging
from typing import Sequence

from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select

from app.core.exceptions import DatabaseOperationError
from app.models.catalog_brand import CatalogBrand

logger = logging.getLogger(__name__)

class CatalogBrandRepository:
    def __init__(self, session: AsyncSession):
        self.session = session

    async def get_by_id(self, brand_id: int) -> CatalogBrand | None:
        try:
            result = await self.session.execute(
                select(CatalogBrand).where(CatalogBrand.id == brand_id)
            )
        except SQLAlchemyError as exc:
            logger.exception("Failed to load catalog brand %s", brand_id)
            raise DatabaseOperationError("Failed to load catalog brand") from exc
        catalog_brand = result.scalar_one_or_none()
        if catalog_brand:
            logger.debug("Catalog brand %s loaded", brand_id)
        else:
            logger.debug("Catalog brand %s not found", brand_id)
        return catalog_brand

    async def list_all(self) -> Sequence[CatalogBrand]:
        try:
            result = await self.session.execute(select(CatalogBrand))
        except SQLAlchemyError as exc:
            logger.exception("Failed to list catalog brands")
            raise DatabaseOperationError("Failed to list catalog brands") from exc
        brands = result.scalars().all()
        logger.debug("Loaded %s catalog brands", len(brands))
        return brands

    async def add(self, catalog_brand: CatalogBrand) -> CatalogBrand:
        self.session.add(catalog_brand)
        try:
            await self.session.commit()
            await self.session.refresh(catalog_brand)
        except SQLAlchemyError as exc:
            await self.session.rollback()
            logger.exception("Failed to create catalog brand")
            raise DatabaseOperationError("Failed to create catalog brand") from exc
        logger.info("Catalog brand %s created", catalog_brand.id)
        return catalog_brand

    async def delete(self, brand_id: int) -> None:
        catalog_brand = await self.get_by_id(brand_id)
        if catalog_brand:
            try:
                await self.session.delete(catalog_brand)
                await self.session.commit()
            except SQLAlchemyError as exc:
                await self.session.rollback()
                logger.exception("Failed to delete catalog brand %s", brand_id)
                raise DatabaseOperationError("Failed to delete catalog brand") from exc
            logger.info("Catalog brand %s deleted", brand_id)
        else:
            logger.warning("Catalog brand %s could not be deleted because it does not exist", brand_id)