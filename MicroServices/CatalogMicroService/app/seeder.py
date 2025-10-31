from sqlmodel import select
from app.models import CatalogBrand, CatalogType, CatalogItem
from sqlalchemy.ext.asyncio import AsyncSession

async def seed_catalog_brands(session: AsyncSession):
    brands_to_add = [
        CatalogBrand(brand="Azure"),
        CatalogBrand(brand=".NET"),
        CatalogBrand(brand="Visual Studio"),
        CatalogBrand(brand="SQL Server"),
        CatalogBrand(brand="Other")
    ]
    
    for brand in brands_to_add:
        result = await session.execute(select(CatalogBrand).where(CatalogBrand.brand == brand.brand))
        if not result.scalars().first():  
            session.add(brand)
    
    await session.commit()

async def seed_catalog_types(session: AsyncSession):
    types_to_add = [
        CatalogType(type="Mug"),
        CatalogType(type="T-Shirt"),
        CatalogType(type="Sheet"),
        CatalogType(type="USB Memory Stick")
    ]
    
    for catalog_type in types_to_add:
        result = await session.execute(select(CatalogType).where(CatalogType.type == catalog_type.type))
        if not result.scalars().first(): 
            session.add(catalog_type)
    
    await session.commit()

async def seed_catalog_items(session: AsyncSession):
    items_to_add = [
        CatalogItem(catalog_type_id=2, catalog_brand_id=2, description=".NET Bot Black Sweatshirt", name=".NET Bot Black Sweatshirt", price=19.5, picture_uri="http://catalogbaseurltobereplaced/images/products/1.png"),
        CatalogItem(catalog_type_id=1, catalog_brand_id=2, description=".NET Black & White Mug", name=".NET Black & White Mug", price=8.50, picture_uri="http://catalogbaseurltobereplaced/images/products/2.png"),
        CatalogItem(catalog_type_id=2, catalog_brand_id=5, description="Prism White T-Shirt", name="Prism White T-Shirt", price=12, picture_uri="http://catalogbaseurltobereplaced/images/products/3.png"),
        CatalogItem(catalog_type_id=2, catalog_brand_id=2, description=".NET Foundation Sweatshirt", name=".NET Foundation Sweatshirt", price=12, picture_uri="http://catalogbaseurltobereplaced/images/products/4.png"),
        CatalogItem(catalog_type_id=3, catalog_brand_id=5, description="Roslyn Red Sheet", name="Roslyn Red Sheet", price=8.5, picture_uri="http://catalogbaseurltobereplaced/images/products/5.png"),
        CatalogItem(catalog_type_id=2, catalog_brand_id=2, description=".NET Blue Sweatshirt", name=".NET Blue Sweatshirt", price=12, picture_uri="http://catalogbaseurltobereplaced/images/products/6.png"),
        CatalogItem(catalog_type_id=2, catalog_brand_id=5, description="Roslyn Red T-Shirt", name="Roslyn Red T-Shirt", price=12, picture_uri="http://catalogbaseurltobereplaced/images/products/7.png"),
        CatalogItem(catalog_type_id=2, catalog_brand_id=5, description="Kudu Purple Sweatshirt", name="Kudu Purple Sweatshirt", price=8.5, picture_uri="http://catalogbaseurltobereplaced/images/products/8.png"),
        CatalogItem(catalog_type_id=1, catalog_brand_id=5, description="Cup<T> White Mug", name="Cup<T> White Mug", price=12, picture_uri="http://catalogbaseurltobereplaced/images/products/9.png"),
        CatalogItem(catalog_type_id=3, catalog_brand_id=2, description=".NET Foundation Sheet", name=".NET Foundation Sheet", price=12, picture_uri="http://catalogbaseurltobereplaced/images/products/10.png"),
        CatalogItem(catalog_type_id=3, catalog_brand_id=2, description="Cup<T> Sheet", name="Cup<T> Sheet", price=8.5, picture_uri="http://catalogbaseurltobereplaced/images/products/11.png"),
        CatalogItem(catalog_type_id=2, catalog_brand_id=5, description="Prism White TShirt", name="Prism White TShirt", price=12, picture_uri="http://catalogbaseurltobereplaced/images/products/12.png")
    ]
    
    for item in items_to_add:
        result = await session.execute(select(CatalogItem).where(CatalogItem.name == item.name))
        if not result.scalars().first():
            session.add(item)
    
    await session.commit()

async def seed_db(session: AsyncSession):
    await seed_catalog_brands(session)
    await seed_catalog_types(session)
    await seed_catalog_items(session)