from typing import Sequence
from sqlalchemy.ext.asyncio import AsyncSession
from sqlmodel import select
from app.models.catalog_brand import CatalogBrand

class CatalogBrandRepository:
    def __init__(self, session: AsyncSession):
        self.session = session

    async def get_by_id(self, brand_id: int) -> CatalogBrand | None:
        result = await self.session.execute(
            select(CatalogBrand).where(CatalogBrand.id == brand_id)
        )
        return result.scalar_one_or_none()

    async def list_all(self) -> Sequence[CatalogBrand]:
        result = await self.session.execute(select(CatalogBrand))
        return result.scalars().all()

    async def add(self, catalog_brand: CatalogBrand) -> CatalogBrand:
        self.session.add(catalog_brand)
        await self.session.commit()
        await self.session.refresh(catalog_brand)
        return catalog_brand

    async def delete(self, brand_id: int) -> None:
        catalog_brand = await self.get_by_id(brand_id)
        if catalog_brand:
            await self.session.delete(catalog_brand)
            await self.session.commit()