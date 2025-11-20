import { Module } from '@nestjs/common';
import { RabbitMQModule } from '@golevelup/nestjs-rabbitmq';
import { TypeOrmModule } from '@nestjs/typeorm';
import { CatalogItemStockService } from './catalog-item-stock.service';
import { CatalogItemStockConsumer } from './catalog-item-stock.consumer';
import { CatalogItemStock } from './entities/catalog-item-stock.entity';
import { Reservation } from './entities/reservation.entity';
import { ScheduleModule } from '@nestjs/schedule';
import { ReservationCleanupCronService } from './cron/reservation-cleanup-cron.service';

@Module({
  imports: [
    ScheduleModule.forRoot(),
    TypeOrmModule.forFeature([CatalogItemStock, Reservation]),
    RabbitMQModule.forRoot({
      exchanges: [{ name: 'catalog_item_stock.exchange', type: 'topic' }],
      uri: process.env.RABBITMQ_URI || 'amqp://guest:guest@localhost:5672',
      connectionInitOptions: { wait: false },
    }),
  ],
  providers: [CatalogItemStockService, CatalogItemStockConsumer, ReservationCleanupCronService],
})
export class CatalogItemStockModule {}
