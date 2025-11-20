import logging

from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select

from app.core.exceptions import DatabaseOperationError
from app.models.catalog_type import CatalogType

logger = logging.getLogger(__name__)

class CatalogTypeRepository:
    def __init__(self, session: AsyncSession):
        self.session = session

    async def get_by_id(self, type_id: int) -> CatalogType | None:
        try:
            result = await self.session.execute(
                select(CatalogType).where(CatalogType.id == type_id)
            )
        except SQLAlchemyError as exc:
            logger.exception("Failed to load catalog type %s", type_id)
            raise DatabaseOperationError("Failed to load catalog type") from exc
        catalog_type = result.scalar_one_or_none()
        if catalog_type:
            logger.debug("Catalog type %s loaded", type_id)
        else:
            logger.debug("Catalog type %s not found", type_id)
        return catalog_type

    async def list_all(self) -> list[CatalogType]:
        try:
            result = await self.session.execute(select(CatalogType))
        except SQLAlchemyError as exc:
            logger.exception("Failed to list catalog types")
            raise DatabaseOperationError("Failed to list catalog types") from exc
        catalog_types = result.scalars().all()
        logger.debug("Loaded %s catalog types", len(catalog_types))
        return catalog_types

    async def add(self, catalog_type: CatalogType) -> CatalogType:
        self.session.add(catalog_type)
        try:
            await self.session.commit()
            await self.session.refresh(catalog_type)
        except SQLAlchemyError as exc:
            await self.session.rollback()
            logger.exception("Failed to create catalog type")
            raise DatabaseOperationError("Failed to create catalog type") from exc
        logger.info("Catalog type %s created", catalog_type.id)
        return catalog_type

    async def delete(self, type_id: int) -> None:
        catalog_type = await self.get_by_id(type_id)
        if catalog_type:
            try:
                await self.session.delete(catalog_type)
                await self.session.commit()
            except SQLAlchemyError as exc:
                await self.session.rollback()
                logger.exception("Failed to delete catalog type %s", type_id)
                raise DatabaseOperationError("Failed to delete catalog type") from exc
            logger.info("Catalog type %s deleted", type_id)
        else:
            logger.warning("Catalog type %s could not be deleted because it does not exist", type_id)