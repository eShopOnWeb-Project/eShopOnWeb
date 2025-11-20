import { CatalogItemStockService } from '../stock/catalog-item-stock.service';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { DataSource, EntityManager } from 'typeorm';

describe('CatalogItemStockService', () => {
  let service: CatalogItemStockService;
  let mockAmqp: jest.Mocked<AmqpConnection>;
  let mockDataSource: jest.Mocked<DataSource>;
  let mockManager: jest.Mocked<EntityManager>;

  beforeEach(() => {
    mockAmqp = {
      publish: jest.fn().mockResolvedValue(undefined),
    } as any;

    mockManager = {
      findOne: jest.fn(),
      create: jest.fn(),
      save: jest.fn().mockImplementation(async (entity) => entity),
    } as any;

    mockDataSource = {
      getRepository: jest.fn().mockReturnValue({
        find: jest.fn().mockResolvedValue([
          { itemId: 1, total: 10, reserved: 2 },
          { itemId: 2, total: 5, reserved: 1 },
        ]),
      }),
      transaction: jest.fn().mockImplementation(async (cb) => {
        return cb(mockManager);
      }),
    } as any;

    service = new CatalogItemStockService(mockAmqp, mockDataSource);
  });

  it('should return all stock items', async () => {
    const stocks = await service.getFullStock();
    expect(stocks).toEqual([
      { itemId: 1, total: 10, reserved: 2 },
      { itemId: 2, total: 5, reserved: 1 },
    ]);
    expect(mockDataSource.getRepository).toHaveBeenCalled();
  });

  it('should publish event after successful restock', async () => {
    const items = [
      { itemId: 1, amount: 3, basketId: 1 },
      { itemId: 2, amount: 4, basketId: 1 },
    ];
    // Mock findOne to return current stock for each item
    mockManager.findOne
      .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 0 })
      .mockResolvedValueOnce({ itemId: 2, total: 20, reserved: 0 });
    mockManager.create.mockImplementation((entity, data) => data ?? []);

    await service.restockAtomic(items);

    expect(mockManager.save).toHaveBeenCalledTimes(2);
    expect(mockAmqp.publish).toHaveBeenCalledWith(
      'catalog_item_stock.exchange',
      'catalog_item_stock.restock.success',
      items
    );
  });

  it('should throw error if not enough stock to reserve', async () => {
    const items = [{ itemId: 99, amount: 50, basketId: 1 }];
    mockManager.findOne.mockResolvedValue({ itemId: 99, total: 10, reserved: 5 });
    mockManager.create.mockImplementation((entity, data) => data ?? []);

    await expect(service.reserveAtomic(items)).rejects.toThrow('Not enough stock');
  });

  it('should reserve stock successfully and publish event', async () => {
    const items = [
      { itemId: 1, amount: 5, basketId: 1 },
      { itemId: 2, amount: 2, basketId: 1 },
    ];
    mockManager.findOne
      .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 2 })
      .mockResolvedValueOnce({ itemId: 2, total: 5, reserved: 1 });
    mockManager.create.mockImplementation((entity, data) => data ?? []);

    await service.reserveAtomic(items);

    expect(mockManager.save).toHaveBeenCalledTimes(2);
    expect(mockAmqp.publish).toHaveBeenCalledWith(
      'catalog_item_stock.exchange',
      'catalog_item_stock.reserve.success',
      items
    );
  });

  it('should confirm stock reservation and publish event', async () => {
    const items = [
      { itemId: 1, amount: 3, basketId: 1 },
      { itemId: 2, amount: 1, basketId: 1 },
    ];
    mockManager.findOne
      .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 5 })
      .mockResolvedValueOnce({ itemId: 2, total: 5, reserved: 2 });
    mockManager.create.mockImplementation((entity, data) => data ?? []);

    await service.confirmAtomic(items);

    expect(mockManager.save).toHaveBeenCalledTimes(2);
    expect(mockAmqp.publish).toHaveBeenCalledWith(
      'catalog_item_stock.exchange',
      'catalog_item_stock.confirm.success',
      items
    );
  });

  it('should throw error if trying to confirm more than reserved', async () => {
    const items = [{ itemId: 1, amount: 10, basketId: 1 }];
    mockManager.findOne.mockResolvedValue({ itemId: 1, total: 10, reserved: 5 });
    mockManager.create.mockImplementation((entity, data) => data ?? []);

    await expect(service.confirmAtomic(items)).rejects.toThrow('Not enough reserved stock');
  });

  it('should cancel reservation and publish event', async () => {
    const items = [
      { itemId: 1, amount: 2, basketId: 1 },
      { itemId: 2, amount: 1, basketId: 1 },
    ];
    mockManager.findOne
      .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 5 })
      .mockResolvedValueOnce({ itemId: 2, total: 5, reserved: 2 });
    mockManager.create.mockImplementation((entity, data) => data ?? []);

    await service.cancelAtomic(items);

    expect(mockManager.save).toHaveBeenCalledTimes(2);
    expect(mockAmqp.publish).toHaveBeenCalledWith(
      'catalog_item_stock.exchange',
      'catalog_item_stock.cancel.success',
      items
    );
  });

  it('should throw error if trying to cancel more than reserved', async () => {
    const items = [{ itemId: 1, amount: 10, basketId: 1 }];
    mockManager.findOne.mockResolvedValue({ itemId: 1, total: 10, reserved: 5 });
    mockManager.create.mockImplementation((entity, data) => data ?? []);

    await expect(service.cancelAtomic(items)).rejects.toThrow('Cannot cancel more than reserved');
  });
});
