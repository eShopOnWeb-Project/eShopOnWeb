from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.database import init_db
from app.routers.catalog_item_router import router as catalog_item_router
from app.routers.catalog_brand_router import router as catalog_brand_router
from app.routers.catalog_type_router import router as catalog_type_router
from contextlib import asynccontextmanager

@asynccontextmanager
async def lifespan(app: FastAPI):
    await init_db()  
    yield             

app = FastAPI(title="Catalog Microservice", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],          
    allow_credentials=False,     
    allow_methods=["*"],
    allow_headers=["*"],         
)

app.include_router(catalog_item_router)
app.include_router(catalog_brand_router)
app.include_router(catalog_type_router)