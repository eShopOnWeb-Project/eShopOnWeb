from pydantic import BaseModel

class DeleteCatalogItemResponse(BaseModel):
    Status: str = "Deleted"