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
      find: jest.fn(), // Mock find for active reservations
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

  describe('restockAtomic', () => {
    it('should publish event after successful restock', async () => {
      const items = [
        { itemId: 1, amount: 3, basketId: 101 },
        { itemId: 2, amount: 4, basketId: 102 },
      ];

      // Mock findOne to return current stock for each item
      mockManager.findOne
        .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 0 })
        .mockResolvedValueOnce({ itemId: 2, total: 20, reserved: 0 });

      // Mock save to correctly update total stock
      mockManager.save.mockImplementation(async (stock) => {
        stock.total += stock.amount; // Ensure total is updated correctly
        return stock;
      });

      await service.restockAtomic(items);

      expect(mockManager.save).toHaveBeenCalledTimes(2);
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.restock.success',
        [
          { itemId: 1, amount: 13, basketId: 101 },  // Updated amount
          { itemId: 2, amount: 24, basketId: 102 },  // Updated amount
        ]
      );
    });
  });

  describe('reserveAtomic', () => {
    it('should reserve stock successfully and publish event', async () => {
      const items = [
        { itemId: 1, amount: 5, basketId: 101 },
        { itemId: 2, amount: 2, basketId: 102 },
      ];

      // Mock findOne to return stock
      mockManager.findOne
        .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 2 })
        .mockResolvedValueOnce({ itemId: 2, total: 5, reserved: 1 });

      mockManager.create.mockImplementation((entity, data) => data ?? []);
      mockManager.save.mockImplementation(async (stock) => stock);

      await service.reserveAtomic(items);

      expect(mockManager.save).toHaveBeenCalledTimes(2); // Ensure it's only called twice (once for each item)
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.reserve.success',
        items
      );
    });

    it('should throw error if not enough stock to reserve', async () => {
      const items = [{ itemId: 99, amount: 50, basketId: 101 }];
      mockManager.findOne.mockResolvedValue({ itemId: 99, total: 10, reserved: 5 });

      await expect(service.reserveAtomic(items)).rejects.toThrow('Not enough stock');
    });
  });

  describe('confirmAtomic', () => {
    it('should confirm stock reservation and publish event', async () => {
      const items = [
        { itemId: 1, amount: 3, basketId: 101 },
        { itemId: 2, amount: 1, basketId: 102 },
      ];

      // Mock find to simulate reservation
      mockManager.find.mockResolvedValue([
        { itemId: 1, basketId: 101, amount: 3, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
        { itemId: 2, basketId: 102, amount: 1, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);
      mockManager.save.mockImplementation(async (stock) => stock);

      await service.confirmAtomic(items);

      expect(mockManager.save).toHaveBeenCalledTimes(2);
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.confirm.success',
        items
      );
    });

    it('should throw error if not enough reserved stock', async () => {
      const items = [{ itemId: 1, amount: 10, basketId: 101 }];
      mockManager.find.mockResolvedValue([
        { itemId: 1, basketId: 101, amount: 5, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);
      mockManager.save.mockImplementation(async (stock) => stock);

      await expect(service.confirmAtomic(items)).rejects.toThrow('Not enough reserved stock');
    });
  });

  describe('cancelAtomic', () => {
    it('should cancel reservation and publish event', async () => {
      const items = [
        { itemId: 1, amount: 2, basketId: 101 },
        { itemId: 2, amount: 1, basketId: 102 },
      ];

      // Mock find to simulate reservation
      mockManager.find.mockResolvedValue([
        { itemId: 1, basketId: 101, amount: 3, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
        { itemId: 2, basketId: 102, amount: 2, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);
      mockManager.save.mockImplementation(async (stock) => stock);

      await service.cancelAtomic(items);

      expect(mockManager.save).toHaveBeenCalledTimes(2);
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.cancel.success',
        items
      );
    });

    it('should throw error if trying to cancel more than reserved', async () => {
      const items = [{ itemId: 1, amount: 10, basketId: 101 }];
      mockManager.find.mockResolvedValue([
        { itemId: 1, basketId: 101, amount: 5, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);
      mockManager.save.mockImplementation(async (stock) => stock);

      await expect(service.cancelAtomic(items)).rejects.toThrow('Cannot cancel more than reserved');
    });
  });

  describe('checkActiveReservations', () => {
    it('should check for active reservations and return missing items', async () => {
      const items = [
        { itemId: 1, amount: 3, basketId: 101 },
        { itemId: 2, amount: 2, basketId: 102 },
      ];

      mockManager.find.mockResolvedValue([
        { itemId: 1, basketId: 101, amount: 3, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
        { itemId: 2, basketId: 102, amount: 1, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);

      const result = await service.checkActiveReservations(items);

      expect(result.success).toBe(false);
      expect(result.missingItems).toEqual([2]);
    });
  });
});
