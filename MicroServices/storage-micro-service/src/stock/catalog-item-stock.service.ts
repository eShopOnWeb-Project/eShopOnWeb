import { Injectable } from '@nestjs/common';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { DataSource, LessThan, MoreThan, In } from 'typeorm';
import { CatalogItemStock } from './entities/catalog-item-stock.entity';
import { Reservation } from './entities/reservation.entity';
import { DefaultDTOItem } from './dto/default-dto-item.interface';
import { FullDTOItem } from './dto/full-dto-item.interface';

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

      const updatedStock: DefaultDTOItem[] = [];
      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        const stock = stocks[i];
        stock.total += item.amount;
        await save(stock);

        updatedStock.push({
          itemId: stock.itemId,
          amount: stock.total,
          basketId: item.basketId,
        });
      }

      await this.publishEvent('restock.success', updatedStock);
    });
  }

  async reserveAtomic(items: DefaultDTOItem[]) {
    await this.withLockedItems(items, async (stocks, save, manager) => {
      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        const stock = stocks[i];
        const available = stock.total - stock.reserved;
        const newReserveAmount = item.amount - stock.reserved;
        if (available < newReserveAmount) {
          throw new Error(`Not enough stock for item ${item.itemId}. Available: ${available}`);
        }
      }

      const results: DefaultDTOItem[] = [];

      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        const stock = stocks[i];

        console.log(`Checking existing reservation for basketId=${item.basketId}, itemId=${item.itemId}`);

        let reservation = await manager.findOne(Reservation, {
          where: {
            basketId: item.basketId,
            itemId: item.itemId,
            status: 'reserved',
            expiresAt: MoreThan(new Date()),
          },
        });

        if (reservation) {
          console.log(`Found existing reservation id=${reservation.id} with amount=${reservation.amount}`);

          const amountDiff = item.amount - reservation.amount;

          if (amountDiff > 0) {
            if ((stock.total - stock.reserved) < amountDiff) {
              throw new Error(`Not enough stock to increase reservation for item ${item.itemId}.`);
            }

            reservation.amount = item.amount;
            console.log(`New Reservation amount for reservation id=${reservation.id} with amount=${reservation.amount}`);
            reservation.expiresAt = new Date(Date.now() + 1 * 60 * 1000);
            await manager.save(reservation);

            stock.reserved += amountDiff;
            console.log(`New Reserved amount for stock id=${stock.id} with amount=${stock.reserved}`);
          } else if (amountDiff < 0) {
            stock.reserved -= Math.abs(amountDiff);

            if (stock.reserved < 0) stock.reserved = 0;

            reservation.amount = item.amount;
            reservation.expiresAt = new Date(Date.now() + 1 * 60 * 1000);

            if (reservation.amount === 0) {
              reservation.status = 'cancelled';
            }

            await manager.save(reservation);
          }

        } else {
          console.log(`No existing reservation found - creating new for basketId=${item.basketId}, itemId=${item.itemId}`);

          reservation = manager.create(Reservation, {
            itemId: item.itemId,
            basketId: item.basketId,
            amount: item.amount,
            expiresAt: new Date(Date.now() + 1 * 60 * 1000),
            status: 'reserved',
          });

          await manager.save(reservation);

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
      for (let i = 0; i < items.length; i++) {
        const item = items[i];
        const stock = stocks[i];

        console.log(`Checking existing reservation for basketId=${item.basketId}, itemId=${item.itemId}`);

        const totalReserved = stock.reserved;
        if (totalReserved < item.amount) {
          throw new Error(`Cannot cancel more than reserved for item ${item.itemId}. Reserved: ${totalReserved}`);
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
          throw new Error(`No active reservation found for item ${item.itemId} in basket ${item.basketId}.`);
        }

        let amountToCancel = item.amount;

        for (const res of reservations) {
          if (amountToCancel <= 0) break;

          if (res.amount > 0) {
            res.status = 'cancelled'; 
            await manager.save(res);

            amountToCancel -= res.amount;
          }
        }

        if (amountToCancel > 0) {
          throw new Error(`Not enough reserved items to cancel for item ${item.itemId}. Cancellation amount exceeds reserved stock.`);
        }

        stock.reserved -= item.amount;
        if (stock.reserved < 0) stock.reserved = 0;

        await save(stock);

        console.log(`Successfully cancelled all reservations for basketId=${item.basketId}, itemId=${item.itemId}, amount=${item.amount}`);
      }

      await this.publishEvent('cancel.success', items);
    });
  }


  async checkActiveReservations(items: DefaultDTOItem[]): Promise<{ success: boolean; missingItems: number[] }> {
    if (items.length === 0) {
      return { success: true, missingItems: [] };
    }

    const basketId = items[0].basketId;

    if (!items.every(item => item.basketId === basketId)) {
      throw new Error('All items must have the same basketId');
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

    const missingItems: number[] = [];

    for (const item of items) {
      const totalReserved = activeReservations
        .filter(r => r.itemId === item.itemId)
        .reduce((sum, r) => sum + r.amount, 0);

      if (totalReserved < item.amount) {
        missingItems.push(item.itemId);
      }
    }

    return {
      success: missingItems.length === 0,
      missingItems,
    };
  }



}
