from typing import Sequence
from sqlalchemy.ext.asyncio import AsyncSession
from app.models.catalog_item import CatalogItem
from sqlalchemy import func
from sqlmodel import select

class CatalogItemRepository:
    def __init__(self, db: AsyncSession):
        self.db = db

    async def get_by_id(self, id: int) -> CatalogItem | None:
        return await self.db.get(CatalogItem, id)

    async def list_catalog_items(
        self,
        db: AsyncSession,
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
        result = await db.execute(stmt)
        return result.scalars().all()

    async def count_catalog_items(
        self,
        db: AsyncSession,
        brand_id: int | None = None,
        type_id: int | None = None
    ) -> int:
        stmt = select(func.count()).select_from(CatalogItem)
        if brand_id is not None:
            stmt = stmt.where(CatalogItem.catalog_brand_id == brand_id)
        if type_id is not None:
            stmt = stmt.where(CatalogItem.catalog_type_id == type_id)
        result = await db.execute(stmt)
        return result.scalar_one()

    async def add(self, item: CatalogItem) -> CatalogItem:
        self.db.add(item)
        await self.db.commit()
        await self.db.refresh(item)
        return item

    async def update(self, item: CatalogItem) -> CatalogItem:
        await self.db.commit()
        await self.db.refresh(item)
        return item

    async def delete(self, item: CatalogItem):
        await self.db.delete(item)
        await self.db.commit()