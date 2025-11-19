import { Injectable, Logger } from '@nestjs/common';
import { Cron, CronExpression } from '@nestjs/schedule';
import { DataSource, LessThan } from 'typeorm';
import { CatalogItemStock } from '../entities/catalog-item-stock.entity';
import { Reservation } from '../entities/reservation.entity';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';

@Injectable()
export class ReservationCleanupCronService {
  private readonly logger = new Logger(ReservationCleanupCronService.name);

  constructor(
    private readonly dataSource: DataSource,
    private readonly amqpConnection: AmqpConnection,
  ) {}

  @Cron(CronExpression.EVERY_MINUTE)
  async handleExpiredReservations() {
    const now = new Date();
    this.logger.debug(`Checking for expired reservations at ${now.toISOString()}`);

    await this.dataSource.transaction(async (manager) => {
      const expiredReservations = await manager.find(Reservation, {
        where: {
          status: 'reserved',
          expiresAt: LessThan(now),
        },
      });

      if (expiredReservations.length === 0) {
        this.logger.debug('No expired reservations found.');
        return;
      }

      this.logger.log(`Found ${expiredReservations.length} expired reservations to release.`);

      for (const res of expiredReservations) {
        const stock = await manager.findOne(CatalogItemStock, {
          where: { itemId: res.itemId },
          lock: { mode: 'pessimistic_write' },
        });

        if (stock) {
          stock.reserved -= res.amount;
          if (stock.reserved < 0) stock.reserved = 0;
          await manager.save(stock);
        } else {
          this.logger.warn(`Stock not found for itemId ${res.itemId} while releasing expired reservation.`);
        }

        res.status = 'cancelled';
        await manager.save(res);

        await this.amqpConnection.publish(
          'catalog_item_stock.exchange',
          'catalog_item_stock.reservation.expired',
          { itemId: res.itemId, amount: res.amount },
        );

        this.logger.log(`Released expired reservation id=${res.id} for itemId=${res.itemId}`);
      }
    });
  }
}