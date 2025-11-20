import { Injectable, Logger } from '@nestjs/common';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { DataSource, LessThan, MoreThan, In, QueryFailedError } from 'typeorm';
import { CatalogItemStock } from './entities/catalog-item-stock.entity';
import { Reservation } from './entities/reservation.entity';
import { DefaultDTOItem } from './dto/default-dto-item.interface';
import { FullDTOItem } from './dto/full-dto-item.interface';
import {
  InsufficientStockError,
  InsufficientReservedStockError,
  ReservationNotFoundError,
  ReservationMismatchError,
  InvalidInputError,
  DatabaseOperationError,
  EventPublishError,
} from './errors/stock-errors';

@Injectable()
export class CatalogItemStockService {
  private readonly logger = new Logger(CatalogItemStockService.name);

  constructor(
    private readonly amqpConnection: AmqpConnection,
    private readonly dataSource: DataSource
  ) {}

  private async withLockedItems<T>(
    items: DefaultDTOItem[],
    work: (
      stocks: CatalogItemStock[],
      save: (s: CatalogItemStock) => Promise<void>,
      manager: any
    ) => Promise<T>,
    options?: { requireBasketId?: boolean }
  ): Promise<T> {
    const { requireBasketId = true } = options ?? {};
    if (!items || items.length === 0) {
      throw new InvalidInputError('Items array cannot be empty');
    }

    for (const item of items) {
      if (!item.itemId || item.itemId <= 0) {
        throw new InvalidInputError(`Invalid itemId: ${item.itemId}`, { item });
      }
      if (item.amount < 0) {
        throw new InvalidInputError(`Invalid amount: ${item.amount} for itemId ${item.itemId}`, { item });
      }
      if (requireBasketId) {
        if (!item.basketId || item.basketId <= 0) {
          throw new InvalidInputError(`Invalid basketId: ${item.basketId} for itemId ${item.itemId}`, { item });
        }
      } else if (item.basketId !== undefined && item.basketId < 0) {
        throw new InvalidInputError(`Invalid basketId: ${item.basketId} for itemId ${item.itemId}`, { item });
      }
    }

    const sortedItems = [...items].sort((a, b) => a.itemId - b.itemId);
    const itemIds = sortedItems.map(i => i.itemId);
    this.logger.debug(`Starting transaction for items: ${itemIds.join(', ')}`);

    try {
      return await this.dataSource.transaction(async (manager) => {
        const lockedStocks: CatalogItemStock[] = [];

        try {
          for (const item of sortedItems) {
            let stock: CatalogItemStock | null;
            try {
              stock = await manager.findOne(CatalogItemStock, {
                where: { itemId: item.itemId },
                lock: { mode: 'pessimistic_write' },
              });

              if (!stock) {
                this.logger.debug(`Creating new stock entry for itemId: ${item.itemId}`);
                stock = manager.create(CatalogItemStock, { itemId: item.itemId, total: 0, reserved: 0 });
                await manager.save(stock);
                this.logger.debug(`Created new stock entry for itemId: ${item.itemId}`);
              }
            } catch (error) {
              this.logger.error(`Failed to find or create stock for itemId ${item.itemId}`, error.stack);
              throw new DatabaseOperationError(`find/create stock for itemId ${item.itemId}`, error, { itemId: item.itemId });
            }

            if (!stock) {
              throw new DatabaseOperationError(`Failed to create stock for itemId ${item.itemId}`, new Error('Stock is null after creation'), { itemId: item.itemId });
            }

            lockedStocks.push(stock);
          }

          const save = async (s: CatalogItemStock) => {
            try {
              await manager.save(s);
            } catch (error) {
              this.logger.error(`Failed to save stock for itemId ${s.itemId}`, error.stack);
              throw new DatabaseOperationError(`save stock for itemId ${s.itemId}`, error, { itemId: s.itemId, stock: s });
            }
          };

          return await work(lockedStocks, save, manager);
        } catch (error) {
          this.logger.error(`Transaction work failed for items: ${itemIds.join(', ')}`, error.stack);
          throw error;
        }
      });
    } catch (error) {
      if (error instanceof QueryFailedError) {
        this.logger.error(`Database query failed for items: ${itemIds.join(', ')}`, error.stack);
        throw new DatabaseOperationError('transaction execution', error, { itemIds });
      }
      throw error;
    }
  }

  async getFullStock(): Promise<FullDTOItem[]> {
    this.logger.debug('Fetching full stock inventory');
    try {
      const stocks = await this.dataSource.getRepository(CatalogItemStock).find();
      const result = stocks.map((s) => ({
        itemId: s.itemId,
        total: s.total,
        reserved: s.reserved,
      }));
      this.logger.debug(`Retrieved ${result.length} stock items`);
      return result;
    } catch (error) {
      this.logger.error('Failed to fetch full stock inventory', error.stack);
      throw new DatabaseOperationError('fetch full stock', error);
    }
  }

  private async publishEvent(event: string, payload: any) {
    const eventName = `catalog_item_stock.${event}`;
    const itemCount = Array.isArray(payload) ? payload.length : 1;
    this.logger.debug(`Publishing event: ${eventName} with ${itemCount} item(s)`);
    
    try {
      await this.amqpConnection.publish(
        'catalog_item_stock.exchange',
        eventName,
        payload
      );
      this.logger.debug(`Successfully published event: ${eventName}`);
    } catch (error) {
      this.logger.error(`Failed to publish event: ${eventName}`, error.stack);
      throw new EventPublishError(eventName, error, { payload });
    }
  }

  async restockAtomic(items: DefaultDTOItem[]) {
    this.logger.log(`Starting restock operation for ${items.length} item(s)`);
    try {
      await this.withLockedItems(
        items,
        async (stocks, save) => {
        const updatedStock: DefaultDTOItem[] = [];
        for (let i = 0; i < items.length; i++) {
          const item = items[i];
          const stock = stocks[i];
          const previousTotal = stock.total;
          stock.total += item.amount;
          await save(stock);

          this.logger.debug(`Restocked itemId ${item.itemId}: ${previousTotal} -> ${stock.total} (added ${item.amount})`);

          updatedStock.push({
            itemId: stock.itemId,
            amount: stock.total,
              basketId: item.basketId ?? null,
          });
        }

        await this.publishEvent('restock.success', updatedStock);
        },
        { requireBasketId: false }
      );
      this.logger.log(`Successfully completed restock operation for ${items.length} item(s)`);
    } catch (error) {
      if (error instanceof InsufficientStockError || error instanceof DatabaseOperationError || error instanceof EventPublishError || error instanceof InvalidInputError) {
        throw error;
      }
      this.logger.error(`Restock operation failed: ${error.message}`, error.stack, {
        itemCount: items.length,
        items: items.map(i => ({ itemId: i.itemId, amount: i.amount, basketId: i.basketId })),
      });
      throw new DatabaseOperationError('restock operation', error, { items });
    }
  }

  async reserveAtomic(items: DefaultDTOItem[]) {
    const basketId = items[0]?.basketId;
    this.logger.log(`Starting reservation operation for basketId ${basketId} with ${items.length} item(s)`);
    try {
      await this.withLockedItems(items, async (stocks, save, manager) => {
        for (let i = 0; i < items.length; i++) {
          const item = items[i];
          const stock = stocks[i];
          const available = stock.total - stock.reserved;
          const newReserveAmount = item.amount - stock.reserved;
          if (available < newReserveAmount) {
            this.logger.warn(`Insufficient stock for itemId ${item.itemId}. Available: ${available}, Requested: ${item.amount}`, {
              itemId: item.itemId,
              available,
              requested: item.amount,
              total: stock.total,
              reserved: stock.reserved,
              basketId: item.basketId,
            });
            throw new InsufficientStockError(item.itemId, available, item.amount, {
              basketId: item.basketId,
              total: stock.total,
              reserved: stock.reserved,
            });
          }
        }

        const results: DefaultDTOItem[] = [];

        for (let i = 0; i < items.length; i++) {
          const item = items[i];
          const stock = stocks[i];

          let reservation = await manager.findOne(Reservation, {
            where: {
              basketId: item.basketId,
              itemId: item.itemId,
              status: 'reserved',
              expiresAt: MoreThan(new Date()),
            },
          });

          if (reservation) {
            this.logger.debug(`Found existing reservation id=${reservation.id} for itemId=${item.itemId} basketId=${item.basketId} with amount=${reservation.amount}`);
            const amountDiff = item.amount - reservation.amount;

            if (amountDiff > 0) {
              const availableForIncrease = stock.total - stock.reserved;
              if (availableForIncrease < amountDiff) {
                this.logger.warn(`Cannot increase reservation for itemId ${item.itemId}: insufficient available stock`, {
                  itemId: item.itemId,
                  basketId: item.basketId,
                  reservationId: reservation.id,
                  currentAmount: reservation.amount,
                  requestedAmount: item.amount,
                  amountDiff,
                  availableForIncrease,
                  total: stock.total,
                  reserved: stock.reserved,
                });
                throw new InsufficientStockError(
                  item.itemId,
                  availableForIncrease,
                  amountDiff,
                  {
                    basketId: item.basketId,
                    reservationId: reservation.id,
                    operation: 'increase_reservation',
                    currentReservationAmount: reservation.amount,
                    requestedAmount: item.amount,
                  }
                );
              }

              reservation.amount = item.amount;
              reservation.expiresAt = new Date(Date.now() + 1 * 60 * 1000);
              await manager.save(reservation);

              stock.reserved += amountDiff;
              this.logger.debug(`Increased reservation id=${reservation.id} for itemId=${item.itemId} by ${amountDiff} (new total: ${reservation.amount})`);
            } else if (amountDiff < 0) {
              stock.reserved -= Math.abs(amountDiff);

              if (stock.reserved < 0) stock.reserved = 0;

              reservation.amount = item.amount;
              reservation.expiresAt = new Date(Date.now() + 1 * 60 * 1000);

              if (reservation.amount === 0) {
                reservation.status = 'cancelled';
                this.logger.debug(`Cancelled reservation id=${reservation.id} for itemId=${item.itemId} (amount became 0)`);
              }

              await manager.save(reservation);
              this.logger.debug(`Decreased reservation id=${reservation.id} for itemId=${item.itemId} by ${Math.abs(amountDiff)} (new total: ${reservation.amount})`);
            }

          } else {
            reservation = manager.create(Reservation, {
              itemId: item.itemId,
              basketId: item.basketId,
              amount: item.amount,
              expiresAt: new Date(Date.now() + 1 * 60 * 1000),
              status: 'reserved',
            });

            await manager.save(reservation);
            this.logger.debug(`Created new reservation for itemId=${item.itemId} basketId=${item.basketId} amount=${item.amount}`);

            stock.reserved += item.amount;
          }

          await save(stock);

          results.push({
            itemId: stock.itemId,
            amount: stock.reserved,
            basketId: reservation.basketId,
          });
        }

        await this.publishEvent('reserve.success', results);
      });
      this.logger.log(`Successfully completed reservation operation for basketId ${basketId}`);
    } catch (error) {
      if (error instanceof InsufficientStockError || error instanceof DatabaseOperationError || error instanceof EventPublishError || error instanceof InvalidInputError) {
        throw error;
      }
      this.logger.error(`Reservation operation failed for basketId ${basketId}: ${error.message}`, error.stack, {
        basketId,
        itemCount: items.length,
        items: items.map(i => ({ itemId: i.itemId, amount: i.amount })),
      });
      throw new DatabaseOperationError('reservation operation', error, { basketId, items });
    }
  }

  async confirmAtomic(items: DefaultDTOItem[]) {
    const basketId = items[0]?.basketId;
    this.logger.log(`Starting confirmation operation for basketId ${basketId} with ${items.length} item(s)`);
    try {
      await this.withLockedItems(items, async (stocks, save, manager) => {
        for (const item of items) {
          const stock = stocks.find(s => s.itemId === item.itemId);
          const totalReserved = stock?.reserved || 0;
          
          if (!stock) {
            this.logger.error(`Stock not found for itemId ${item.itemId} during confirmation`, {
              itemId: item.itemId,
              basketId: item.basketId,
              requestedAmount: item.amount,
            });
            throw new InvalidInputError(`Stock not found for itemId ${item.itemId}`, {
              itemId: item.itemId,
              basketId: item.basketId,
            });
          }

          if (totalReserved < item.amount) {
            this.logger.warn(`Insufficient reserved stock for itemId ${item.itemId}. Reserved: ${totalReserved}, Requested: ${item.amount}`, {
              itemId: item.itemId,
              basketId: item.basketId,
              reserved: totalReserved,
              requested: item.amount,
              total: stock.total,
            });
            throw new InsufficientReservedStockError(item.itemId, totalReserved, item.amount, {
              basketId: item.basketId,
              total: stock.total,
            });
          }

          let amountToConfirm = item.amount;

          const reservations = await manager.find(Reservation, {
            where: { itemId: item.itemId, status: 'reserved' },
            order: { expiresAt: 'ASC' },
            lock: { mode: 'pessimistic_write' },
          });

          this.logger.debug(`Found ${reservations.length} reservation(s) for itemId ${item.itemId}`);

          for (const res of reservations) {
            if (amountToConfirm <= 0) break;
            const usedAmount = Math.min(res.amount, amountToConfirm);

            res.amount -= usedAmount;
            if (res.amount === 0) {
              res.status = 'confirmed';
            } else {
              const newRes = manager.create(Reservation, {
                itemId: res.itemId,
                amount: res.amount,
                expiresAt: res.expiresAt,
                status: 'reserved',
              });
              await manager.save(newRes);

              res.amount = usedAmount;
              res.status = 'confirmed';
            }
            await manager.save(res);
            this.logger.debug(`Confirmed reservation id=${res.id} for itemId=${item.itemId}, usedAmount=${usedAmount}`);

            amountToConfirm -= usedAmount;
          }

          if (amountToConfirm > 0) {
            this.logger.error(`Reservation mismatch for itemId ${item.itemId}: ${amountToConfirm} remaining unconfirmed`, {
              itemId: item.itemId,
              basketId: item.basketId,
              requestedAmount: item.amount,
              remainingUnconfirmed: amountToConfirm,
              reservationsFound: reservations.length,
              reservationsProcessed: reservations.filter(r => r.status === 'confirmed').length,
            });
            throw new ReservationMismatchError(item.itemId, amountToConfirm, {
              basketId: item.basketId,
              requestedAmount: item.amount,
              reservationsFound: reservations.length,
            });
          }
        }

        for (let i = 0; i < items.length; i++) {
          const item = items[i];
          const stock = stocks[i];
          const previousReserved = stock.reserved;
          const previousTotal = stock.total;
          stock.reserved -= item.amount;
          stock.total -= item.amount;
          if (stock.reserved < 0) stock.reserved = 0;
          if (stock.total < 0) stock.total = 0;
          await save(stock);
          this.logger.debug(`Updated stock for itemId ${item.itemId}: reserved ${previousReserved} -> ${stock.reserved}, total ${previousTotal} -> ${stock.total}`);
        }

        await this.publishEvent('confirm.success', items);
      });
      this.logger.log(`Successfully completed confirmation operation for basketId ${basketId}`);
    } catch (error) {
      if (error instanceof InsufficientReservedStockError || error instanceof ReservationMismatchError || error instanceof DatabaseOperationError || error instanceof EventPublishError || error instanceof InvalidInputError) {
        throw error;
      }
      this.logger.error(`Confirmation operation failed for basketId ${basketId}: ${error.message}`, error.stack, {
        basketId,
        itemCount: items.length,
        items: items.map(i => ({ itemId: i.itemId, amount: i.amount })),
      });
      throw new DatabaseOperationError('confirmation operation', error, { basketId, items });
    }
  }

  async cancelAtomic(items: DefaultDTOItem[]) {
    const basketId = items[0]?.basketId;
    this.logger.log(`Starting cancellation operation for basketId ${basketId} with ${items.length} item(s)`);
    try {
      await this.withLockedItems(items, async (stocks, save, manager) => {
        for (let i = 0; i < items.length; i++) {
          const item = items[i];
          const stock = stocks[i];
          const totalReserved = stock.reserved;
          if (totalReserved < item.amount) {
            this.logger.warn(`Cannot cancel more than reserved for itemId ${item.itemId}. Reserved: ${totalReserved}, Requested: ${item.amount}`, {
              itemId: item.itemId,
              basketId: item.basketId,
              reserved: totalReserved,
              requested: item.amount,
              total: stock.total,
            });
            throw new InsufficientReservedStockError(item.itemId, totalReserved, item.amount, {
              basketId: item.basketId,
              operation: 'cancel',
              total: stock.total,
            });
          }

          const reservations = await manager.find(Reservation, {
            where: {
              itemId: item.itemId,
              basketId: item.basketId,
              status: 'reserved',
            },
            lock: { mode: 'pessimistic_write' },
          });

          if (reservations.length === 0) {
            this.logger.warn(`No active reservation found for itemId ${item.itemId} in basketId ${item.basketId}`, {
              itemId: item.itemId,
              basketId: item.basketId,
              requestedAmount: item.amount,
              totalReserved: stock.reserved,
            });
            throw new ReservationNotFoundError(item.itemId, item.basketId, {
              requestedAmount: item.amount,
              totalReserved: stock.reserved,
            });
          }

          this.logger.debug(`Found ${reservations.length} reservation(s) to cancel for itemId ${item.itemId}`);

          let amountToCancel = item.amount;

          for (const res of reservations) {
            if (amountToCancel <= 0) break;

            if (res.amount > 0) {
              res.status = 'cancelled';
              await manager.save(res);
              this.logger.debug(`Cancelled reservation id=${res.id} for itemId=${item.itemId} amount=${res.amount}`);

              amountToCancel -= res.amount;
            }
          }

          if (amountToCancel > 0) {
            this.logger.error(`Cancellation incomplete for itemId ${item.itemId}: ${amountToCancel} remaining`, {
              itemId: item.itemId,
              basketId: item.basketId,
              requestedAmount: item.amount,
              remainingUncancelled: amountToCancel,
              reservationsFound: reservations.length,
              totalReserved: stock.reserved,
            });
            throw new ReservationMismatchError(item.itemId, amountToCancel, {
              basketId: item.basketId,
              operation: 'cancel',
              requestedAmount: item.amount,
              reservationsFound: reservations.length,
            });
          }

          const previousReserved = stock.reserved;
          stock.reserved -= item.amount;
          if (stock.reserved < 0) stock.reserved = 0;

          await save(stock);
          this.logger.debug(`Updated stock for itemId ${item.itemId}: reserved ${previousReserved} -> ${stock.reserved}`);
        }

        await this.publishEvent('cancel.success', items);
      });
      this.logger.log(`Successfully completed cancellation operation for basketId ${basketId}`);
    } catch (error) {
      if (error instanceof InsufficientReservedStockError || error instanceof ReservationNotFoundError || error instanceof ReservationMismatchError || error instanceof DatabaseOperationError || error instanceof EventPublishError || error instanceof InvalidInputError) {
        throw error;
      }
      this.logger.error(`Cancellation operation failed for basketId ${basketId}: ${error.message}`, error.stack, {
        basketId,
        itemCount: items.length,
        items: items.map(i => ({ itemId: i.itemId, amount: i.amount })),
      });
      throw new DatabaseOperationError('cancellation operation', error, { basketId, items });
    }
  }

  async checkActiveReservations(items: DefaultDTOItem[]): Promise<{ success: boolean; missingItems: number[] }> {
    if (items.length === 0) {
      this.logger.debug('Checking active reservations: empty items array');
      return { success: true, missingItems: [] };
    }

    const basketId = items[0].basketId;
    this.logger.debug(`Checking active reservations for basketId ${basketId} with ${items.length} item(s)`);

    if (!items.every(item => item.basketId === basketId)) {
      const basketIds = [...new Set(items.map(i => i.basketId))];
      this.logger.error('All items must have the same basketId', {
        expectedBasketId: basketId,
        foundBasketIds: basketIds,
        items: items.map(i => ({ itemId: i.itemId, basketId: i.basketId })),
      });
      throw new InvalidInputError('All items must have the same basketId', {
        expectedBasketId: basketId,
        foundBasketIds: basketIds,
      });
    }

    const manager = this.dataSource.manager;

    const activeReservations = await manager.find(Reservation, {
      where: {
        basketId: basketId,
        itemId: In(items.map(i => i.itemId)),
        status: 'reserved',
        expiresAt: MoreThan(new Date()),
      },
    });

    this.logger.debug(`Found ${activeReservations.length} active reservation(s) for basketId ${basketId}`);

    const missingItems: number[] = [];

    for (const item of items) {
      const totalReserved = activeReservations
        .filter(r => r.itemId === item.itemId)
        .reduce((sum, r) => sum + r.amount, 0);

      if (totalReserved < item.amount) {
        this.logger.debug(`Missing reservation for itemId ${item.itemId}: reserved ${totalReserved}, required ${item.amount}`);
        missingItems.push(item.itemId);
      }
    }

    const result = {
      success: missingItems.length === 0,
      missingItems,
    };

    if (result.success) {
      this.logger.debug(`All reservations active for basketId ${basketId}`);
    } else {
      this.logger.warn(`Missing reservations for basketId ${basketId}: itemIds ${missingItems.join(', ')}`);
    }

    return result;
  }
}
