import { Module } from '@nestjs/common';
import { RabbitMQModule } from '@golevelup/nestjs-rabbitmq';
import { TypeOrmModule } from '@nestjs/typeorm';
import { CatalogItemStockService } from './catalog-item-stock.service';
import { CatalogItemStockConsumer } from './catalog-item-stock.consumer';
import { CatalogItemStock } from './entities/catalog-item-stock.entity';
import { TestStockController } from './catalog-item-rabbitmq-test-controller';

@Module({
  imports: [
    TypeOrmModule.forFeature([CatalogItemStock]),
    RabbitMQModule.forRoot({
      exchanges: [{ name: 'catalog_item_stock.exchange', type: 'topic' }],
      uri: process.env.RABBITMQ_URI || 'amqp://guest:guest@localhost:5672',
      connectionInitOptions: { wait: false },
    }),
  ],
  controllers: [TestStockController],
  providers: [CatalogItemStockService, CatalogItemStockConsumer],
})
export class CatalogItemStockModule {}
