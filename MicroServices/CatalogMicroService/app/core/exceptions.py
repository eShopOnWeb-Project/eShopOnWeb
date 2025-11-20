class ServiceError(Exception):
    """Base class for domain/service level errors surfaced to API clients."""

    status_code = 500
    message = "Internal server error"

    def __init__(self, message: str | None = None, *, status_code: int | None = None):
        if message:
            self.message = message
        if status_code:
            self.status_code = status_code
        super().__init__(self.message)


class DatabaseOperationError(ServiceError):
    """Raised when a database operation fails."""

    def __init__(self, message: str = "Database operation failed"):
        super().__init__(message, status_code=500)


class NotFoundError(ServiceError):
    """Raised when a resource should exist but does not."""

    def __init__(self, message: str = "Resource not found"):
        super().__init__(message, status_code=404)

