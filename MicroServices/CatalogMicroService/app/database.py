import asyncio
import logging
import os

from dotenv import load_dotenv
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine
from sqlmodel import SQLModel

from app.core.exceptions import DatabaseOperationError
from app.seeder import seed_db

if not logging.getLogger().handlers:
    logging.basicConfig(
        level=os.getenv("LOG_LEVEL", "INFO"),
        format="%(asctime)s %(name)s [%(levelname)s] %(message)s",
    )

logger = logging.getLogger(__name__)

load_dotenv()

DATABASE_URL = os.getenv("DATABASE_URL")
if not DATABASE_URL:
    raise ValueError("DATABASE_URL is not set in .env")

engine = create_async_engine(DATABASE_URL, echo=True)

async_session: async_sessionmaker[AsyncSession] = async_sessionmaker(
    bind=engine,
    expire_on_commit=False,
)

async def get_db():
    async with async_session() as session:
        yield session

async def init_db():
    try:
        async with engine.begin() as conn:
            await conn.run_sync(SQLModel.metadata.drop_all)
            await conn.run_sync(SQLModel.metadata.create_all)

        logger.info("Database schema recreated")

        async with async_session() as session:
            await seed_db(session)
        logger.info("Database seeding complete")
    except (SQLAlchemyError, DatabaseOperationError) as exc:
        logger.exception("Failed to initialize database")
        raise DatabaseOperationError("Failed to initialize database") from exc

if __name__ == "__main__":
    asyncio.run(init_db())
