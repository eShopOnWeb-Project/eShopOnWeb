from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from app.models.catalog_type import CatalogType

class CatalogTypeRepository:
    def __init__(self, session: AsyncSession):
        self.session = session

    async def get_by_id(self, type_id: int) -> CatalogType | None:
        result = await self.session.execute(
            select(CatalogType).where(CatalogType.id == type_id)
        )
        return result.scalar_one_or_none()

    async def list_all(self) -> list[CatalogType]:
        result = await self.session.execute(select(CatalogType))
        return result.scalars().all()

    async def add(self, catalog_type: CatalogType) -> CatalogType:
        self.session.add(catalog_type)
        await self.session.commit()
        await self.session.refresh(catalog_type)
        return catalog_type

    async def delete(self, type_id: int) -> None:
        catalog_type = await self.get_by_id(type_id)
        if catalog_type:
            await self.session.delete(catalog_type)
            await self.session.commit()