import { Injectable } from '@nestjs/common';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { DataSource, LessThan } from 'typeorm';
import { CatalogItemStock } from './entities/catalog-item-stock.entity';
import { Reservation } from './entities/reservation.entity';
import { DefaultDTOItem, FullDTOItem } from './catalog-item-stock.consumer';

@Injectable()
export class CatalogItemStockService {
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
    ) => Promise<T>
  ): Promise<T> {
    const sortedItems = [...items].sort((a, b) => a.itemId - b.itemId);

    return this.dataSource.transaction(async (manager) => {
      const lockedStocks: CatalogItemStock[] = [];

      for (const item of sortedItems) {
        let stock = await manager.findOne(CatalogItemStock, {
          where: { itemId: item.itemId },
          lock: { mode: 'pessimistic_write' },
        });

        if (!stock) {
          stock = manager.create(CatalogItemStock, { itemId: item.itemId, total: 0, reserved: 0 });
          await manager.save(stock);
        }

        lockedStocks.push(stock);
      }

      const save = async (s: CatalogItemStock) => {
        await manager.save(s);
      };

      return work(lockedStocks, save, manager);
    });
  }

  async getFullStock(): Promise<FullDTOItem[]> {
    const stocks = await this.dataSource.getRepository(CatalogItemStock).find();
    return stocks.map((s) => ({
      itemId: s.itemId,
      total: s.total,
      reserved: s.reserved,
    }));
  }

  private async publishEvent(event: string, payload: any) {
    await this.amqpConnection.publish(
      'catalog_item_stock.exchange',
      `catalog_item_stock.${event}`,
      payload
    );
  }

  async restockAtomic(items: DefaultDTOItem[]) {
    await this.withLockedItems(items, async (stocks, save) => {
      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        const stock = stocks[i];
        stock.total += item.amount;
        await save(stock);
      }

      await this.publishEvent('restock.success', items);
    });
  }

  async reserveAtomic(items: DefaultDTOItem[]) {
    await this.withLockedItems(items, async (stocks, save, manager) => {
      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        const stock = stocks[i];
        const available = stock.total - stock.reserved;
        if (available < item.amount) {
          throw new Error(`Not enough stock for item ${item.itemId}. Available: ${available}`);
        }
      }

      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        const stock = stocks[i];
        stock.reserved += item.amount;
        await save(stock);

        const reservation = manager.create(Reservation, {
          itemId: item.itemId,
          amount: item.amount,
          expiresAt: new Date(Date.now() + 5 * 60 * 1000),
          status: 'reserved',
        });
        await manager.save(reservation);
      }

      await this.publishEvent('reserve.success', items);
    });
  }

  async confirmAtomic(items: DefaultDTOItem[]) {
    await this.withLockedItems(items, async (stocks, save, manager) => {
      for (const item of items) {
        const totalReserved = stocks.find(s => s.itemId === item.itemId)?.reserved || 0;
        if (totalReserved < item.amount) {
          throw new Error(`Not enough reserved stock for item ${item.itemId}. Reserved: ${totalReserved}`);
        }

        let amountToConfirm = item.amount;

        const reservations = await manager.find(Reservation, {
          where: { itemId: item.itemId, status: 'reserved' },
          order: { expiresAt: 'ASC' },
          lock: { mode: 'pessimistic_write' },
        });

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

          amountToConfirm -= usedAmount;
        }

        if (amountToConfirm > 0) {
          throw new Error(`Reservation mismatch for item ${item.itemId}, not enough reserved amount.`);
        }
      }

      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        const stock = stocks[i];
        stock.reserved -= item.amount;
        stock.total -= item.amount;
        if (stock.reserved < 0) stock.reserved = 0;
        if (stock.total < 0) stock.total = 0;
        await save(stock);
      }

      await this.publishEvent('confirm.success', items);
    });
  }

  async cancelAtomic(items: DefaultDTOItem[]) {
    await this.withLockedItems(items, async (stocks, save, manager) => {
      for (const item of items) {
        const totalReserved = stocks.find(s => s.itemId === item.itemId)?.reserved || 0;
        if (totalReserved < item.amount) {
          throw new Error(`Cannot cancel more than reserved for item ${item.itemId}. Reserved: ${totalReserved}`);
        }

        let amountToCancel = item.amount;

        const reservations = await manager.find(Reservation, {
          where: { itemId: item.itemId, status: 'reserved' },
          order: { expiresAt: 'ASC' },
          lock: { mode: 'pessimistic_write' },
        });

        for (const res of reservations) {
          if (amountToCancel <= 0) break;

          const usedAmount = Math.min(res.amount, amountToCancel);
          res.amount -= usedAmount;

          if (res.amount === 0) {
            res.status = 'cancelled';
          } else {
            const newRes = manager.create(Reservation, {
              itemId: res.itemId,
              amount: res.amount,
              expiresAt: res.expiresAt,
              status: 'reserved',
            });
            await manager.save(newRes);

            res.amount = usedAmount;
            res.status = 'cancelled';
          }
          await manager.save(res);

          amountToCancel -= usedAmount;
        }

        if (amountToCancel > 0) {
          throw new Error(`Reservation mismatch for item ${item.itemId}, not enough reserved amount to cancel.`);
        }
      }

      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        const stock = stocks[i];
        stock.reserved -= item.amount;
        if (stock.reserved < 0) stock.reserved = 0;
        await save(stock);
      }

      await this.publishEvent('cancel.success', items);
    });
  }
}
