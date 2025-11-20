import logging
from typing import Sequence

from sqlalchemy import func
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select

from app.core.exceptions import DatabaseOperationError
from app.models.catalog_item import CatalogItem

logger = logging.getLogger(__name__)

class CatalogItemRepository:
    def __init__(self, db: AsyncSession):
        self.db = db

    async def get_by_id(self, id: int) -> CatalogItem | None:
        try:
            item = await self.db.get(CatalogItem, id)
        except SQLAlchemyError as exc:
            logger.exception("Failed to load catalog item %s", id)
            raise DatabaseOperationError("Failed to load catalog item") from exc

        if item:
            logger.debug("Catalog item %s loaded", id)
        else:
            logger.debug("Catalog item %s not found", id)
        return item

    async def list_catalog_items(
        self,
        skip: int = 0,
        take: int = 10,
        brand_id: int | None = None,
        type_id: int | None = None
    ) -> Sequence[CatalogItem]:
        stmt = select(CatalogItem)
        if brand_id is not None:
            stmt = stmt.where(CatalogItem.catalog_brand_id == brand_id)
        if type_id is not None:
            stmt = stmt.where(CatalogItem.catalog_type_id == type_id)
        stmt = stmt.offset(skip).limit(take)
        try:
            result = await self.db.execute(stmt)
        except SQLAlchemyError as exc:
            logger.exception("Failed to list catalog items")
            raise DatabaseOperationError("Failed to list catalog items") from exc
        logger.debug(
            "Listing catalog items skip=%s take=%s brand_id=%s type_id=%s",
            skip,
            take,
            brand_id,
            type_id,
        )
        return result.scalars().all()

    async def count_catalog_items(
        self,
        brand_id: int | None = None,
        type_id: int | None = None
    ) -> int:
        stmt = select(func.count()).select_from(CatalogItem)
        if brand_id is not None:
            stmt = stmt.where(CatalogItem.catalog_brand_id == brand_id)
        if type_id is not None:
            stmt = stmt.where(CatalogItem.catalog_type_id == type_id)
        try:
            result = await self.db.execute(stmt)
        except SQLAlchemyError as exc:
            logger.exception("Failed to count catalog items")
            raise DatabaseOperationError("Failed to count catalog items") from exc
        count = result.scalar_one()
        logger.debug(
            "Counted %s catalog items for brand_id=%s type_id=%s",
            count,
            brand_id,
            type_id,
        )
        return count

    async def add(self, item: CatalogItem) -> CatalogItem:
        self.db.add(item)
        try:
            await self.db.commit()
            await self.db.refresh(item)
        except SQLAlchemyError as exc:
            await self.db.rollback()
            logger.exception("Failed to create catalog item")
            raise DatabaseOperationError("Failed to create catalog item") from exc
        logger.info("Catalog item %s created", item.id)
        return item

    async def update(self, item: CatalogItem) -> CatalogItem:
        try:
            await self.db.commit()
            await self.db.refresh(item)
        except SQLAlchemyError as exc:
            await self.db.rollback()
            logger.exception("Failed to update catalog item %s", item.id)
            raise DatabaseOperationError("Failed to update catalog item") from exc
        logger.info("Catalog item %s updated", item.id)
        return item

    async def delete(self, item: CatalogItem) -> None:
        try:
            await self.db.delete(item)
            await self.db.commit()
        except SQLAlchemyError as exc:
            await self.db.rollback()
            logger.exception("Failed to delete catalog item %s", item.id)
            raise DatabaseOperationError("Failed to delete catalog item") from exc
        logger.info("Catalog item %s deleted", item.id)