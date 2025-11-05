import pytest
from app.models.catalog_item import CatalogItem
from app.repositories.catalog_item_repository import CatalogItemRepository

@pytest.mark.asyncio
async def test_add_and_get(db_session):  # db_session er allerede en AsyncSession, ikke en generator
    repo = CatalogItemRepository(db_session)

    item = CatalogItem(
        name="Test Item",
        description="Description",
        price=9.99,
        catalog_brand_id=1,
        catalog_type_id=1
    )
    added = await repo.add(item)
    assert added.id is not None

    fetched = await repo.get_by_id(added.id)
    assert fetched is not None
    assert fetched.name == "Test Item"
