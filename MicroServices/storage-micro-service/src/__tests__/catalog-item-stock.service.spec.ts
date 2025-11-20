import { CatalogItemStockService } from '../stock/catalog-item-stock.service';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { DataSource } from 'typeorm';
import { Reservation } from '../stock/entities/reservation.entity';
import { CatalogItemStock } from '../stock/entities/catalog-item-stock.entity';
import { DefaultDTOItem } from '../stock/dto/default-dto-item.interface';

describe('CatalogItemStockService', () => {
  let service: CatalogItemStockService;
  let mockManager: any;
  let mockAmqp: any;

  beforeEach(() => {
    mockManager = {
      findOne: jest.fn(),
      find: jest.fn(),
      save: jest.fn(),
    };

    mockAmqp = {
      publish: jest.fn(),
    };

    service = new CatalogItemStockService(mockAmqp, mockManager as unknown as DataSource);
  });

  it('should reserve stock successfully and publish event', async () => {
    const mockStocks = [{ itemId: 1, total: 100, reserved: 20 }];
    const mockItems: DefaultDTOItem[] = [{ itemId: 1, amount: 10, basketId: 101 }];

    // Mock the necessary methods
    mockManager.findOne.mockResolvedValue(null);  // No existing reservation
    mockManager.save.mockResolvedValue(undefined); // Simulate save

    await service.reserveAtomic(mockItems);

    // Validate that the save method was called
    expect(mockManager.save).toHaveBeenCalledTimes(2);  // Adjust according to the number of calls
    expect(mockAmqp.publish).toHaveBeenCalledWith(
      'catalog_item_stock.exchange',
      'catalog_item_stock.reserve.success',
      expect.arrayContaining([{ itemId: 1, amount: 10, basketId: 101 }])
    );
  });

  it('should confirm stock reservation successfully', async () => {
    const mockStocks = [{ itemId: 1, total: 100, reserved: 20 }];
    const mockItems: DefaultDTOItem[] = [{ itemId: 1, amount: 5, basketId: 101 }];

    mockManager.find.mockResolvedValue(mockStocks); // Find stock
    mockManager.findOne.mockResolvedValue({ id: 1, itemId: 1, basketId: 101, amount: 5, status: 'reserved', expiresAt: new Date() });

    await service.confirmAtomic(mockItems);

    expect(mockManager.save).toHaveBeenCalledTimes(2);  // Adjust according to the number of save calls
  });

  it('should throw error if not enough reserved stock', async () => {
    const mockItems: DefaultDTOItem[] = [{ itemId: 1, amount: 10, basketId: 101 }];
    const mockStocks = [{ itemId: 1, total: 100, reserved: 5 }];

    mockManager.find.mockResolvedValue(mockStocks);

    await expect(service.confirmAtomic(mockItems)).rejects.toThrow('Not enough reserved stock');
  });

  it('should cancel reservation and publish event', async () => {
    const mockItems: DefaultDTOItem[] = [{ itemId: 1, amount: 5, basketId: 101 }];
    const mockStocks = [{ itemId: 1, total: 100, reserved: 20 }];

    mockManager.find.mockResolvedValue(mockStocks); // Find stock
    mockManager.findOne.mockResolvedValue({ id: 1, itemId: 1, basketId: 101, amount: 5, status: 'reserved', expiresAt: new Date() });

    await service.cancelAtomic(mockItems);

    expect(mockManager.save).toHaveBeenCalledTimes(2);  // Adjust according to the number of save calls
    expect(mockAmqp.publish).toHaveBeenCalledWith(
      'catalog_item_stock.exchange',
      'catalog_item_stock.cancel.success',
      expect.arrayContaining([{ itemId: 1, amount: 5, basketId: 101 }])
    );
  });

  it('should throw error if trying to cancel more than reserved', async () => {
    const mockItems: DefaultDTOItem[] = [{ itemId: 1, amount: 30, basketId: 101 }];
    const mockStocks = [{ itemId: 1, total: 100, reserved: 20 }];

    mockManager.find.mockResolvedValue(mockStocks);

    await expect(service.cancelAtomic(mockItems)).rejects.toThrow('Not enough reserved items to cancel');
  });

  it('should check active reservations and return missing items', async () => {
    const mockItems: DefaultDTOItem[] = [{ itemId: 1, amount: 5, basketId: 101 }];
    const mockReservations = [
      { itemId: 1, basketId: 101, amount: 5, status: 'reserved', expiresAt: new Date() },
    ];

    mockManager.find.mockResolvedValue(mockReservations);

    const result = await service.checkActiveReservations(mockItems);

    expect(result.success).toBe(true);
    expect(result.missingItems).toHaveLength(0);
  });

  it('should throw error if items have different basketIds', async () => {
    const mockItems: DefaultDTOItem[] = [
      { itemId: 1, amount: 5, basketId: 101 },
      { itemId: 2, amount: 3, basketId: 102 }, // Different basketId
    ];

    await expect(service.checkActiveReservations(mockItems)).rejects.toThrow('All items must have the same basketId');
  });
});
