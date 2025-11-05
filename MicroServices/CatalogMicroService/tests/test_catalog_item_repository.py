import pytest
from app.models.catalog_item import CatalogItem
from app.repositories.catalog_item_repository import CatalogItemRepository

@pytest.mark.asyncio
async def test_add_and_get(db_session):
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

@pytest.mark.asyncio
async def test_list_catalog_items(db_session):
    repo = CatalogItemRepository(db_session)

    # Insert items
    items = [
        CatalogItem(name=f"Item {i}", description="Desc", price=10.0+i, catalog_brand_id=i%2 + 1, catalog_type_id=i%3 + 1)
        for i in range(15)
    ]
    for item in items:
        await repo.add(item)

    # List without filters, default skip=0, take=10
    result = await repo.list_catalog_items(db_session)
    assert len(result) == 10

    # List with skip=10, take=5 (should get remaining 5)
    result = await repo.list_catalog_items(db_session, skip=10, take=5)
    assert len(result) == 5

    # List filtered by brand_id=1
    filtered = await repo.list_catalog_items(db_session, brand_id=1)
    assert all(item.catalog_brand_id == 1 for item in filtered)

    # List filtered by type_id=2
    filtered = await repo.list_catalog_items(db_session, type_id=2)
    assert all(item.catalog_type_id == 2 for item in filtered)

@pytest.mark.asyncio
async def test_count_catalog_items(db_session):
    repo = CatalogItemRepository(db_session)

    # Clear DB by dropping and creating again (optional)
    # Or rely on clean test DB setup

    # Insert items
    items = [
        CatalogItem(name=f"CountItem {i}", description="Desc", price=20.0+i, catalog_brand_id=1, catalog_type_id=1 if i%2 == 0 else 2)
        for i in range(6)
    ]
    for item in items:
        await repo.add(item)

    total = await repo.count_catalog_items(db_session)
    assert total >= 6

    count_brand_1 = await repo.count_catalog_items(db_session, brand_id=1)
    assert count_brand_1 >= 6

    count_type_1 = await repo.count_catalog_items(db_session, type_id=1)
    assert count_type_1 > 0
    assert count_type_1 <= count_brand_1

@pytest.mark.asyncio
async def test_update_catalog_item(db_session):
    repo = CatalogItemRepository(db_session)

    item = CatalogItem(
        name="Update Test",
        description="Old Desc",
        price=5.0,
        catalog_brand_id=1,
        catalog_type_id=1
    )
    added = await repo.add(item)

    # Change values
    added.description = "New Desc"
    added.price = 10.0

    updated = await repo.update(added)
    assert updated.description == "New Desc"
    assert updated.price == 10.0

    # Verify persisted
    fetched = await repo.get_by_id(added.id)
    assert fetched.description == "New Desc"
    assert fetched.price == 10.0

@pytest.mark.asyncio
async def test_delete_catalog_item(db_session):
    repo = CatalogItemRepository(db_session)

    item = CatalogItem(
        name="Delete Test",
        description="To be deleted",
        price=1.0,
        catalog_brand_id=1,
        catalog_type_id=1
    )
    added = await repo.add(item)

    await repo.delete(added)

    deleted = await repo.get_by_id(added.id)
    assert deleted is None
