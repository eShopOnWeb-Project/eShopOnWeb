import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { CatalogItemStockModule } from './stock/catalog-item-stock.module';


@Module({
  imports: [
    TypeOrmModule.forRoot({
      type: 'postgres',
      host: process.env.DATABASE_HOST || 'storage-db',
      port: +(process.env.DATABASE_PORT || 5432),
      username: process.env.DATABASE_USER || 'postgres',
      password: process.env.DATABASE_PASSWORD || 'postgres',
      database: process.env.DATABASE_NAME || 'storagedb',
      autoLoadEntities: true,
      synchronize: true, // only for development
    }),
    CatalogItemStockModule,
  ],
})
export class AppModule {}
