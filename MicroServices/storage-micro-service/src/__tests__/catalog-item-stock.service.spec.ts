import { CatalogItemStockService } from '../stock/catalog-item-stock.service';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { DataSource, EntityManager } from 'typeorm';
import { CatalogItemStock } from '../stock/entities/catalog-item-stock.entity';
import { Reservation } from '../stock/entities/reservation.entity';
import { DefaultDTOItem } from '../stock/dto/default-dto-item.interface';

jest.mock('@golevelup/nestjs-rabbitmq');

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
      find: jest.fn(),
      create: jest.fn(),
      save: jest.fn().mockImplementation(async (entity) => entity),
    } as any;

    mockDataSource = {
      transaction: jest.fn().mockImplementation(async (cb) => cb(mockManager)),
      manager: mockManager,
    } as any;

    service = new CatalogItemStockService(mockAmqp, mockDataSource);
  });

  describe('restockAtomic', () => {
    it('should restock items and publish event', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 5, basketId: 101 },
        { itemId: 2, amount: 3, basketId: 102 },
      ];

      // Mock stock retrieval
      mockManager.findOne
        .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 0 })
        .mockResolvedValueOnce({ itemId: 2, total: 20, reserved: 0 });

      // Run the restock method
      await service.restockAtomic(items);

      // Validate stock updates and publish event
      expect(mockManager.save).toHaveBeenCalledTimes(2);
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.restock.success',
        [
          { itemId: 1, amount: 15, basketId: 101 },
          { itemId: 2, amount: 23, basketId: 102 },
        ]
      );
    });
  });

  describe('reserveAtomic', () => {
    it('should reserve stock successfully and publish event', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 5, basketId: 101 },
        { itemId: 2, amount: 3, basketId: 102 },
      ];

      // Mock stock and reservation retrieval
      mockManager.findOne
        .mockResolvedValueOnce({ itemId: 1, total: 10, reserved: 2 })
        .mockResolvedValueOnce({ itemId: 2, total: 20, reserved: 5 });

      mockManager.create.mockImplementation((entity, data) => {
        // Return an array of objects with a random `id` (to simulate entity creation)
        return [{ ...data, id: Math.floor(Math.random() * 1000) }];
      });

      mockManager.save.mockImplementation(async (stock) => stock);

      // Run the reserve method
      await service.reserveAtomic(items);

      // Validate the reservation process
      expect(mockManager.save).toHaveBeenCalledTimes(2);
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.reserve.success',
        [
          { itemId: 1, amount: 5, basketId: 101 },
          { itemId: 2, amount: 3, basketId: 102 },
        ]
      );
    });

    it('should throw error if not enough stock to reserve', async () => {
      const items: DefaultDTOItem[] = [{ itemId: 1, amount: 15, basketId: 101 }];
      mockManager.findOne.mockResolvedValue({ itemId: 1, total: 10, reserved: 5 });

      // Run the reserve method and expect an error
      await expect(service.reserveAtomic(items)).rejects.toThrow('Not enough stock');
    });
  });

  describe('confirmAtomic', () => {
    it('should confirm stock reservation successfully', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 5, basketId: 101 },
        { itemId: 2, amount: 3, basketId: 102 },
      ];

      // Mock stock and reservation retrieval
      mockManager.find.mockResolvedValueOnce([
        { itemId: 1, basketId: 101, amount: 5, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
        { itemId: 2, basketId: 102, amount: 3, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);
      mockManager.save.mockImplementation(async (stock) => stock);

      // Run the confirm method
      await service.confirmAtomic(items);

      // Validate the confirmation process
      expect(mockManager.save).toHaveBeenCalledTimes(4);  // Save reservation + stock update
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.confirm.success',
        items
      );
    });

    it('should throw error if not enough reserved stock', async () => {
      const items: DefaultDTOItem[] = [{ itemId: 1, amount: 15, basketId: 101 }];
      mockManager.find.mockResolvedValue([
        { itemId: 1, basketId: 101, amount: 5, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);

      // Run the confirm method and expect an error
      await expect(service.confirmAtomic(items)).rejects.toThrow('Not enough reserved stock');
    });
  });

  describe('cancelAtomic', () => {
    it('should cancel reservation and publish event', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 3, basketId: 101 },
        { itemId: 2, amount: 2, basketId: 102 },
      ];

      // Mock reservation retrieval
      mockManager.find.mockResolvedValue([
        { itemId: 1, basketId: 101, amount: 3, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
        { itemId: 2, basketId: 102, amount: 2, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);
      mockManager.save.mockImplementation(async (stock) => stock);

      // Run the cancel method
      await service.cancelAtomic(items);

      // Validate the cancellation process
      expect(mockManager.save).toHaveBeenCalledTimes(4);  // 2 for reservation + 2 for stock update
      expect(mockAmqp.publish).toHaveBeenCalledWith(
        'catalog_item_stock.exchange',
        'catalog_item_stock.cancel.success',
        items
      );
    });

    it('should throw error if trying to cancel more than reserved', async () => {
      const items: DefaultDTOItem[] = [{ itemId: 1, amount: 10, basketId: 101 }];
      mockManager.find.mockResolvedValue([
        { itemId: 1, basketId: 101, amount: 5, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);

      // Run the cancel method and expect an error
      await expect(service.cancelAtomic(items)).rejects.toThrow('Not enough reserved items to cancel');
    });
  });

  describe('checkActiveReservations', () => {
    it('should check active reservations and return missing items', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 3, basketId: 101 },
        { itemId: 2, amount: 2, basketId: 102 },
      ];

      // Mock active reservations
      mockManager.find.mockResolvedValue([
        { itemId: 1, basketId: 101, amount: 3, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
        { itemId: 2, basketId: 102, amount: 1, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);

      const result = await service.checkActiveReservations(items);

      // Validate missing items check
      expect(result.success).toBe(false);
      expect(result.missingItems).toEqual([2]);
    });

    it('should return success if all items are reserved', async () => {
      const items: DefaultDTOItem[] = [
        { itemId: 1, amount: 3, basketId: 101 },
        { itemId: 2, amount: 2, basketId: 102 },
      ];

      // Mock active reservations
      mockManager.find.mockResolvedValue([
        { itemId: 1, basketId: 101, amount: 3, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
        { itemId: 2, basketId: 102, amount: 2, status: 'reserved', expiresAt: new Date(Date.now() + 1000) },
      ]);

      const result = await service.checkActiveReservations(items);

      // Validate success
      expect(result.success).toBe(true);
      expect(result.missingItems).toEqual([]);
    });
  });
});
