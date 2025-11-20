import pytest
import asyncio
from sqlmodel import SQLModel
from sqlalchemy.ext.asyncio import create_async_engine, AsyncSession
from sqlalchemy.orm import sessionmaker
from app.database import get_db
from app.main import app
import pytest_asyncio
import os
import sys

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

DATABASE_URL_TEST = "sqlite+aiosqlite:///./test.db"

engine_test = create_async_engine(DATABASE_URL_TEST, echo=False, future=True)
async_session_test = sessionmaker(engine_test, class_=AsyncSession, expire_on_commit=False)

@pytest.fixture(scope="session")
def event_loop():
    loop = asyncio.new_event_loop()
    yield loop
    loop.close()

@pytest_asyncio.fixture(scope="session", autouse=True)
async def prepare_db():
    if os.path.exists("test.db"):
        os.remove("test.db")

    async with engine_test.begin() as conn:
        await conn.run_sync(SQLModel.metadata.create_all)
    yield

@pytest_asyncio.fixture
async def db_session():
    async with async_session_test() as session:
        yield session
