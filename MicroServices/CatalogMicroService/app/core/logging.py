import logging
import os

LOG_FORMAT = "%(asctime)s %(name)s [%(levelname)s] %(message)s"


def configure_logging() -> None:
    """Initialize root logging if no handlers exist yet."""
    if logging.getLogger().handlers:
        return

    level = os.getenv("LOG_LEVEL", "INFO").upper()
    logging.basicConfig(level=level, format=LOG_FORMAT)
    logging.getLogger("uvicorn.error").setLevel(level)
    logging.getLogger("uvicorn.access").setLevel(level)

