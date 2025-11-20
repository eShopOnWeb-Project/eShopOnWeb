import { CatalogItemStockService } from '../stock/catalog-item-stock.service';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { DataSource, EntityManager } from 'typeorm';
import { CatalogItemStock } from '../stock/entities/catalog-item-stock.entity';
import { Reservation } from '../stock/entities/reservation.entity';
import { DefaultDTOItem } from '../stock/dto/default-dto-item.interface';
import { FullDTOItem } from '../stock/dto/full-dto-item.interface';

jest.mock('@golevelup/nestjs-rabbitmq');
jest.mock('typeorm');

describe('CatalogItemStockService', () => {
  let service: CatalogItemStockService;
  let mockAmqpConnection: jest.Mocked<AmqpConnection>;
  let mockDataSource: jest.Mocked<DataSource>;
  let mockManager: jest.Mocked<EntityManager>;

  beforeEach(() => {
    mockAmqpConnection = {
      publish: jest.fn().mockResolvedValue(undefined),
    } as any;

    mockManager = {
      findOne: jest.fn(),
      save: jest.fn().mockResolvedValue(undefined),
      find: jest.fn(),
      create: jest.fn(),
    } as any;

    mockDataSource = {
      getRepository: jest.fn().mockReturnValue({
        find: jest.fn().mockResolvedValue([
          { itemId: 1, total: 10, reserved: 2 },
          { itemId: 2, total: 5, reserved: 1 },
        ]),
      }),
      manager: mockManager,
      transaction: jest.fn().mockImplementation(async (cb) => cb(mockManager)),
    } as any;

    service = new CatalogItemStockService(mockAmqpConnection, mockDataSource);
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
    it('should restock items and publish event', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 5, basketId: 1 },
        { itemId: 2, amount: 3, basketId: 1 },
      ];

      mockManager.findOne
        .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 2 })
        .mockResolvedValueOnce({ itemId: 2, total: 5, reserved: 1 });

      await service.restockAtomic(items);

      expect(mockManager.save).toHaveBeenCalledTimes(2);
      expect(mockAmqpConnection.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.restock.success',
        [
          { itemId: 1, amount: 15, basketId: 1 },
          { itemId: 2, amount: 8, basketId: 1 },
        ]
      );
    });

    it('should throw an error when attempting to restock more than available stock', async () => {
      const items: DefaultDTOItem[] = [{ itemId: 1, amount: 100, basketId: 1 }];

      mockManager.findOne.mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 2 });

      await expect(service.restockAtomic(items)).rejects.toThrow('Stock limit exceeded');
    });
  });

  describe('reserveAtomic', () => {
    it('should reserve stock correctly and publish event', async () => {
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
      expect(mockAmqpConnection.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.reserve.success',
        [
          { itemId: 1, amount: 5, basketId: 1 },
          { itemId: 2, amount: 3, basketId: 1 },
        ]
      );
    });

    it('should throw an error when not enough stock to reserve', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 6, basketId: 1 },
      ];

      mockManager.findOne.mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 5 });

      await expect(service.reserveAtomic(items)).rejects.toThrow('Not enough stock for item 1. Available: 5');
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

      expect(mockManager.save).toHaveBeenCalledTimes(4);
      expect(mockAmqpConnection.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.confirm.success',
        items
      );
    });

    it('should throw an error if reserved stock is insufficient', async () => {
      const items: DefaultDTOItem[] = [{ itemId: 1, amount: 10, basketId: 1 }];

      mockManager.findOne.mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 3 });
      mockManager.find.mockResolvedValueOnce([{ itemId: 1, amount: 3, status: 'reserved', basketId: 1 }]);

      await expect(service.confirmAtomic(items)).rejects.toThrow('Not enough reserved stock for item 1. Reserved: 3');
    });
  });

  describe('cancelAtomic', () => {
    it('should cancel reservations and reduce stock accordingly', async () => {
      const items: DefaultDTOItem[] = [{ itemId: 1, amount: 3, basketId: 1 }];

      mockManager.findOne.mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 5 });
      mockManager.find.mockResolvedValueOnce([{ itemId: 1, amount: 3, status: 'reserved', basketId: 1 }]);

      await service.cancelAtomic(items);

      expect(mockManager.save).toHaveBeenCalledTimes(2);
      expect(mockAmqpConnection.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.cancel.success',
        items
      );
    });

    it('should throw an error if trying to cancel more than reserved stock', async () => {
      const items: DefaultDTOItem[] = [{ itemId: 1, amount: 6, basketId: 1 }];

      mockManager.findOne.mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 5 });
      mockManager.find.mockResolvedValueOnce([{ itemId: 1, amount: 5, status: 'reserved', basketId: 1 }]);

      await expect(service.cancelAtomic(items)).rejects.toThrow('Not enough reserved items to cancel for item 1. Cancellation amount exceeds reserved stock.');
    });
  });

  describe('checkActiveReservations', () => {
    it('should return success if all items are reserved', async () => {
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
      const items: DefaultDTOItem[] = [{ itemId: 1, amount: 5, basketId: 1 }];

      mockManager.find.mockResolvedValueOnce([{ itemId: 1, amount: 3, status: 'reserved', basketId: 1 }]);

      const result = await service.checkActiveReservations(items);
      expect(result.success).toBe(false);
      expect(result.missingItems).toEqual([1]);
    });
  });
});
