import pytest
import asyncio
from sqlmodel import SQLModel
from sqlalchemy.ext.asyncio import create_async_engine, AsyncSession
from sqlalchemy.orm import sessionmaker
from app.database import get_db
from fastapi import FastAPI
from httpx import AsyncClient
from app.main import app

DATABASE_URL_TEST = "sqlite+aiosqlite:///:memory:"

engine_test = create_async_engine(DATABASE_URL_TEST, echo=False)
async_session_test = sessionmaker(engine_test, class_=AsyncSession, expire_on_commit=False)

@pytest.fixture(scope="session")
def event_loop():
    loop = asyncio.get_event_loop()
    yield loop
    loop.close()

@pytest.fixture(scope="session", autouse=True)
async def prepare_db():
    async with engine_test.begin() as conn:
        await conn.run_sync(SQLModel.metadata.create_all)
    yield
    async with engine_test.begin() as conn:
        await conn.run_sync(SQLModel.metadata.drop_all)

@pytest.fixture
async def db_session():
    async with async_session_test() as session:
        yield session

@pytest.fixture
def app_with_test_db():
    async def override_get_db():
        async with async_session_test() as session:
            yield session
    app.dependency_overrides[get_db] = override_get_db
    return app

@pytest.fixture
async def async_client(app_with_test_db: FastAPI):
    async with AsyncClient(app=app_with_test_db, base_url="http://testserver") as client:
        yield client
