export class StockError extends Error {
  constructor(
    message: string,
    public readonly code: string,
    public readonly context?: Record<string, any>
  ) {
    super(message);
    this.name = this.constructor.name;
    Error.captureStackTrace(this, this.constructor);
  }
}

export class InsufficientStockError extends StockError {
  constructor(itemId: number, available: number, requested: number, context?: Record<string, any>) {
    super(
      `Insufficient stock for item ${itemId}. Available: ${available}, Requested: ${requested}`,
      'INSUFFICIENT_STOCK',
      { itemId, available, requested, ...context }
    );
  }
}

export class InsufficientReservedStockError extends StockError {
  constructor(itemId: number, reserved: number, requested: number, context?: Record<string, any>) {
    super(
      `Insufficient reserved stock for item ${itemId}. Reserved: ${reserved}, Requested: ${requested}`,
      'INSUFFICIENT_RESERVED_STOCK',
      { itemId, reserved, requested, ...context }
    );
  }
}

export class ReservationNotFoundError extends StockError {
  constructor(itemId: number, basketId: number, context?: Record<string, any>) {
    super(
      `No active reservation found for item ${itemId} in basket ${basketId}`,
      'RESERVATION_NOT_FOUND',
      { itemId, basketId, ...context }
    );
  }
}

export class ReservationMismatchError extends StockError {
  constructor(itemId: number, remaining: number, context?: Record<string, any>) {
    super(
      `Reservation mismatch for item ${itemId}. ${remaining} amount remaining unconfirmed`,
      'RESERVATION_MISMATCH',
      { itemId, remaining, ...context }
    );
  }
}

export class InvalidInputError extends StockError {
  constructor(message: string, context?: Record<string, any>) {
    super(message, 'INVALID_INPUT', context);
  }
}

export class DatabaseOperationError extends StockError {
  constructor(operation: string, originalError: Error, context?: Record<string, any>) {
    super(
      `Database operation failed: ${operation}. ${originalError.message}`,
      'DATABASE_OPERATION_ERROR',
      { operation, originalError: originalError.message, ...context }
    );
  }
}

export class EventPublishError extends StockError {
  constructor(event: string, originalError: Error, context?: Record<string, any>) {
    super(
      `Failed to publish event: ${event}. ${originalError.message}`,
      'EVENT_PUBLISH_ERROR',
      { event, originalError: originalError.message, ...context }
    );
  }
}

