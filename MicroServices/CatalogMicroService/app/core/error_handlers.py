import logging

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from app.core.exceptions import ServiceError

logger = logging.getLogger("catalog.errors")


def register_exception_handlers(app: FastAPI) -> None:
    @app.exception_handler(ServiceError)
    async def handle_service_error(request: Request, exc: ServiceError):
        logger.warning(
            "ServiceError on %s %s: %s", request.method, request.url.path, exc.message
        )
        return JSONResponse(
            status_code=exc.status_code,
            content={"detail": exc.message},
        )

    @app.exception_handler(Exception)
    async def handle_unexpected_error(request: Request, exc: Exception):
        logger.exception("Unhandled error on %s %s", request.method, request.url.path)
        return JSONResponse(
            status_code=500,
            content={"detail": "Unexpected error occurred. Please try again later."},
        )

