import pytest

@pytest.mark.asyncio
async def test_create_and_get_item(async_client):  # async_client er allerede en AsyncClient-instans
    item_data = {
        "name": "Test Router Item",
        "description": "Desc",
        "price": 19.99,
        "catalog_brand_id": 1,
        "catalog_type_id": 1
    }
    response = await async_client.post("/items", json=item_data)
    assert response.status_code == 200
    data = response.json()
    assert data["name"] == item_data["name"]

    item_id = data["id"]
    get_response = await async_client.get(f"/items/{item_id}")
    assert get_response.status_code == 200
    get_data = get_response.json()
    assert get_data["id"] == item_id
