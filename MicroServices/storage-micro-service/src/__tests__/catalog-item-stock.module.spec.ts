import { Test, TestingModule } from '@nestjs/testing';
import { TypeOrmModule } from '@nestjs/typeorm';
import { CatalogItemStockModule } from '../stock/catalog-item-stock.module';
import { CatalogItemStockService } from '../stock/catalog-item-stock.service';
import { CatalogItemStock } from '../stock/entities/catalog-item-stock.entity';

describe('CatalogItemStockModule', () => {
  let module: TestingModule;

  beforeAll(async () => {
    module = await Test.createTestingModule({
      imports: [
        TypeOrmModule.forRoot({
          type: 'sqlite',
          database: ':memory:',
          entities: [CatalogItemStock],
          synchronize: true,
          logging: false,
        }),
        CatalogItemStockModule,
      ],
    }).compile();
  });

  it('should compile and provide CatalogItemStockService', () => {
    const service = module.get<CatalogItemStockService>(CatalogItemStockService);
    expect(service).toBeDefined();
  });

  afterAll(async () => {
    await module.close();
  });
});
