# logging_config.py
import logging
import json
import sys

class JsonFormatter(logging.Formatter):
    """Format logs as structured JSON lines"""
    def format(self, record):
        log_object = {
            "message": record.getMessage(),
            "level": record.levelname,
            "logger": record.name,
            "service": "orders-service",  # or "catalog-service"
        }

        # Optional extras for more context
        if record.exc_info:
            log_object["exception"] = self.formatException(record.exc_info)
        if record.__dict__.get("request_id"):
            log_object["request_id"] = record.request_id

        return json.dumps(log_object)

def setup_logging():
    """Configure global root logger"""
    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(JsonFormatter())

    root = logging.getLogger()
    root.setLevel(logging.INFO)
    root.handlers = [handler]

    # Prevent duplicate logs from libraries
    logging.getLogger("uvicorn").propagate = False
    logging.getLogger("fastapi").propagate = False
