import { CatalogItemStockService } from '../stock/catalog-item-stock.service';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { DataSource, EntityManager } from 'typeorm';
import { DefaultDTOItem } from '../stock/dto/default-dto-item.interface';

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
      find: jest.fn(),
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
      manager: mockManager,
    } as any;

    service = new CatalogItemStockService(mockAmqp, mockDataSource);
  });

  describe('getFullStock', () => {
    it('should return the full stock of items', async () => {
      const result = await service.getFullStock();
      expect(result).toEqual([
        { itemId: 1, total: 10, reserved: 2 },
        { itemId: 2, total: 5, reserved: 1 },
      ]);
    });
  });

  describe('restockAtomic', () => {
    it('should correctly restock items and publish event', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 5, basketId: 1 },
        { itemId: 2, amount: 3, basketId: 1 },
      ];

      mockManager.findOne
        .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 2 })
        .mockResolvedValueOnce({ itemId: 2, total: 5, reserved: 1 });

      await service.restockAtomic(items);

      expect(mockManager.save).toHaveBeenCalledTimes(2);
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.restock.success',
        [
          { itemId: 1, amount: 15, basketId: 1 }, // Updated total for Item 1
          { itemId: 2, amount: 8, basketId: 1 },  // Updated total for Item 2
        ]
      );
    });

    it('should throw error when restocking more than available stock', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 100, basketId: 1 }, // Trying to restock more than available
      ];

      mockManager.findOne.mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 2 });

      // Use `rejects.toThrow()` here for promises that reject with an error
      await expect(service.restockAtomic(items)).rejects.toThrow('Stock limit exceeded');
    });
  });

  describe('reserveAtomic', () => {
    it('should reserve stock and publish event', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 3, basketId: 1 },
        { itemId: 2, amount: 2, basketId: 1 },
      ];

      mockManager.findOne
        .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 2 })
        .mockResolvedValueOnce({ itemId: 2, total: 5, reserved: 1 });

      mockManager.find.mockResolvedValueOnce([]); // No existing reservations

      await service.reserveAtomic(items);

      expect(mockManager.save).toHaveBeenCalledTimes(2);
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.reserve.success',
        [
          { itemId: 1, amount: 5, basketId: 1 },  // Updated reserved amount for Item 1
          { itemId: 2, amount: 3, basketId: 1 },  // Updated reserved amount for Item 2
        ]
      );
    });

    it('should throw error when not enough stock to reserve', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 5, basketId: 1 },
      ];

      mockManager.findOne.mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 6 });

      // Use `rejects.toThrow()` to test promise rejection
      await expect(service.reserveAtomic(items)).rejects.toThrow('Not enough stock for item 1. Available: 4');
    });
  });

  describe('confirmAtomic', () => {
    it('should confirm reservations and reduce stock accordingly', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 3, basketId: 1 },
        { itemId: 2, amount: 2, basketId: 1 },
      ];

      mockManager.findOne
        .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 5 })
        .mockResolvedValueOnce({ itemId: 2, total: 5, reserved: 3 });

      mockManager.find.mockResolvedValueOnce([
        { itemId: 1, amount: 3, status: 'reserved', basketId: 1 },
        { itemId: 2, amount: 2, status: 'reserved', basketId: 1 },
      ]);

      await service.confirmAtomic(items);

      expect(mockManager.save).toHaveBeenCalledTimes(4);  // Save stock and reservations
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.confirm.success',
        items
      );
    });

    it('should throw error if reserved stock is insufficient', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 10, basketId: 1 },
      ];

      mockManager.findOne.mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 3 });
      mockManager.find.mockResolvedValueOnce([
        { itemId: 1, amount: 3, status: 'reserved', basketId: 1 },
      ]);

      // Use `rejects.toThrow()` for promises that reject
      await expect(service.confirmAtomic(items)).rejects.toThrow('Reservation mismatch for item 1, not enough reserved amount.');
    });
  });

  describe('cancelAtomic', () => {
    it('should cancel reservations and publish event', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 3, basketId: 1 },
      ];

      mockManager.findOne
        .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 5 });

      mockManager.find.mockResolvedValueOnce([
        { itemId: 1, amount: 3, status: 'reserved', basketId: 1 },
      ]);

      await service.cancelAtomic(items);

      expect(mockManager.save).toHaveBeenCalledTimes(2);  // Save stock and update reservations
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.cancel.success',
        items
      );
    });

    it('should throw error if trying to cancel more than reserved stock', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 6, basketId: 1 },  // Trying to cancel more than reserved
      ];

      mockManager.findOne.mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 5 });
      mockManager.find.mockResolvedValueOnce([
        { itemId: 1, amount: 5, status: 'reserved', basketId: 1 },
      ]);

      // Use `rejects.toThrow()` to test promise rejection
      await expect(service.cancelAtomic(items)).rejects.toThrow('Not enough reserved items to cancel for item 1. Cancellation amount exceeds reserved stock.');
    });
  });

  describe('checkActiveReservations', () => {
    it('should check if all items have sufficient reservations', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 3, basketId: 1 },
        { itemId: 2, amount: 2, basketId: 1 },
      ];

      mockManager.find.mockResolvedValueOnce([
        { itemId: 1, amount: 3, status: 'reserved', basketId: 1 },
        { itemId: 2, amount: 2, status: 'reserved', basketId: 1 },
      ]);

      const result = await service.checkActiveReservations(items);
      expect(result.success).toBe(true);
      expect(result.missingItems).toEqual([]);
    });

    it('should return missing items if not enough reservations', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 5, basketId: 1 },
      ];

      mockManager.find.mockResolvedValueOnce([
        { itemId: 1, amount: 3, status: 'reserved', basketId: 1 },
      ]);

      const result = await service.checkActiveReservations(items);
      expect(result.success).toBe(false);
      expect(result.missingItems).toEqual([1]);
    });

    it('should throw error if items have different basketIds', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 3, basketId: 1 },
        { itemId: 2, amount: 2, basketId: 2 }, // Different basketId
      ];

      await expect(service.checkActiveReservations(items)).rejects.toThrow('All items must have the same basketId');
    });
  });
});

