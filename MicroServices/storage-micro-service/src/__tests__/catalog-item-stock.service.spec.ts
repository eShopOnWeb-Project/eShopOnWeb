import { CatalogItemStockService } from '../stock/catalog-item-stock.service';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { DataSource, EntityManager } from 'typeorm';
import { DefaultDTOItem } from '../stock/dto/default-dto-item.interface';
import { CatalogItemStock } from '../stock/entities/catalog-item-stock.entity';
import { Reservation } from '../stock/entities/reservation.entity';

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
      getRepository: jest.fn().mockReturnValue({
        find: jest.fn().mockResolvedValue([
          { itemId: 1, total: 10, reserved: 2 },
          { itemId: 2, total: 5, reserved: 1 },
        ]),
      }),
      manager: mockManager,
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
    const items: DefaultDTOItem[] = [
      { itemId: 1, amount: 3, basketId: 1 },
      { itemId: 2, amount: 4, basketId: 1 },
    ];
    const stock1 = { id: 1, itemId: 1, total: 10, reserved: 0 };
    const stock2 = { id: 2, itemId: 2, total: 20, reserved: 0 };
    mockManager.findOne
      .mockResolvedValueOnce(stock1)
      .mockResolvedValueOnce(stock2);
    mockManager.create.mockImplementation((entity, data) => data ?? ({} as any));

    const updatedStock: DefaultDTOItem[] = [
      { itemId: 1, amount: 13, basketId: 1 },
      { itemId: 2, amount: 24, basketId: 1 },
    ];

    await service.restockAtomic(items);

    expect(mockManager.save).toHaveBeenCalledTimes(2);
    expect(mockAmqp.publish).toHaveBeenCalledWith(
      'catalog_item_stock.exchange',
      'catalog_item_stock.restock.success',
      updatedStock
    );
  });

  it('should throw error if not enough stock to reserve', async () => {
    const items = [{ itemId: 99, amount: 50, basketId: 1 }];
    const stock = { id: 1, itemId: 99, total: 10, reserved: 5 };
    mockManager.findOne
      .mockResolvedValueOnce(stock)
      .mockResolvedValueOnce(null);
    mockManager.create.mockImplementation((entity, data) => data ?? ({} as any));

    await expect(service.reserveAtomic(items)).rejects.toThrow(/Insufficient stock|Not enough stock/);
  });

  it('should reserve stock successfully when no existing reservation and publish event', async () => {
    const items = [
      { itemId: 1, amount: 5, basketId: 1 },
      { itemId: 2, amount: 2, basketId: 1 },
    ];
    const stock1 = { id: 1, itemId: 1, total: 10, reserved: 2 };
    const stock2 = { id: 2, itemId: 2, total: 5, reserved: 1 };
    
    mockManager.findOne.mockImplementation((entity, options: any) => {
      if (entity === CatalogItemStock) {
        const itemId = options?.where?.itemId;
        if (itemId === 1) return Promise.resolve(stock1);
        if (itemId === 2) return Promise.resolve(stock2);
      }
      if (entity === Reservation) {
        return Promise.resolve(null);
      }
      return Promise.resolve(null);
    });

    mockManager.create.mockImplementation((entity, data) => {
      return data as any;
    });

    await service.reserveAtomic(items);

    expect(mockManager.save).toHaveBeenCalledTimes(4);
    expect(stock1.reserved).toBe(7);
    expect(stock2.reserved).toBe(3);
    
    const expectedResults: DefaultDTOItem[] = [
      { itemId: 1, amount: 7, basketId: 1 },
      { itemId: 2, amount: 3, basketId: 1 },
    ];

    expect(mockAmqp.publish).toHaveBeenCalledWith(
      'catalog_item_stock.exchange',
      'catalog_item_stock.reserve.success',
      expectedResults
    );
  });

  it('should update existing reservation when amount increases', async () => {
    const items = [{ itemId: 1, amount: 7, basketId: 1 }];
    const stock = { id: 1, itemId: 1, total: 10, reserved: 2 };
    const existingReservation = { id: 1, itemId: 1, basketId: 1, amount: 5, status: 'reserved', expiresAt: new Date(Date.now() + 60000) };
    
    mockManager.findOne
      .mockResolvedValueOnce(stock)
      .mockResolvedValueOnce(existingReservation);

    await service.reserveAtomic(items);

    expect(mockManager.save).toHaveBeenCalledTimes(2);
    expect(existingReservation.amount).toBe(7);
    expect(stock.reserved).toBe(4);
  });

  it('should update existing reservation when amount decreases', async () => {
    const items = [{ itemId: 1, amount: 3, basketId: 1 }];
    const stock = { id: 1, itemId: 1, total: 10, reserved: 7 };
    const existingReservation = { id: 1, itemId: 1, basketId: 1, amount: 5, status: 'reserved', expiresAt: new Date(Date.now() + 60000) };
    
    mockManager.findOne
      .mockResolvedValueOnce(stock)
      .mockResolvedValueOnce(existingReservation);

    await service.reserveAtomic(items);

    expect(mockManager.save).toHaveBeenCalledTimes(2);
    expect(existingReservation.amount).toBe(3);
    expect(stock.reserved).toBe(5);
  });

  it('should confirm stock reservation and publish event', async () => {
    const items = [
      { itemId: 1, amount: 3, basketId: 1 },
      { itemId: 2, amount: 1, basketId: 1 },
    ];
    const stock1 = { id: 1, itemId: 1, total: 10, reserved: 5 };
    const stock2 = { id: 2, itemId: 2, total: 5, reserved: 2 };
    
    mockManager.findOne
      .mockResolvedValueOnce(stock1)
      .mockResolvedValueOnce(stock2);

    const reservation1 = { id: 1, itemId: 1, basketId: 1, amount: 3, status: 'reserved', expiresAt: new Date() };
    const reservation2 = { id: 2, itemId: 2, basketId: 1, amount: 1, status: 'reserved', expiresAt: new Date() };
    
    mockManager.find
      .mockResolvedValueOnce([reservation1])
      .mockResolvedValueOnce([reservation2]);

    await service.confirmAtomic(items);

    expect(mockManager.save).toHaveBeenCalled();
    expect(reservation1.status).toBe('confirmed');
    expect(reservation2.status).toBe('confirmed');
    expect(stock1.reserved).toBe(2);
    expect(stock1.total).toBe(7);
    expect(stock2.reserved).toBe(1);
    expect(stock2.total).toBe(4);
    expect(mockAmqp.publish).toHaveBeenCalledWith(
      'catalog_item_stock.exchange',
      'catalog_item_stock.confirm.success',
      items
    );
  });

  it('should throw error if trying to confirm more than reserved', async () => {
    const items = [{ itemId: 1, amount: 10, basketId: 1 }];
    const stock = { id: 1, itemId: 1, total: 10, reserved: 5 };
    
    mockManager.findOne.mockResolvedValueOnce(stock);
    mockManager.find.mockResolvedValueOnce([]);

    await expect(service.confirmAtomic(items)).rejects.toThrow(/Insufficient reserved stock|Not enough reserved stock/);
  });

  it('should cancel reservation and publish event', async () => {
    const items = [
      { itemId: 1, amount: 2, basketId: 1 },
      { itemId: 2, amount: 1, basketId: 1 },
    ];
    const stock1 = { id: 1, itemId: 1, total: 10, reserved: 5 };
    const stock2 = { id: 2, itemId: 2, total: 5, reserved: 2 };
    
    mockManager.findOne
      .mockResolvedValueOnce(stock1)
      .mockResolvedValueOnce(stock2);

    const reservation1 = { id: 1, itemId: 1, basketId: 1, amount: 2, status: 'reserved', expiresAt: new Date() };
    const reservation2 = { id: 2, itemId: 2, basketId: 1, amount: 1, status: 'reserved', expiresAt: new Date() };
    
    mockManager.find
      .mockResolvedValueOnce([reservation1])
      .mockResolvedValueOnce([reservation2]);

    await service.cancelAtomic(items);

    expect(mockManager.save).toHaveBeenCalled();
    expect(reservation1.status).toBe('cancelled');
    expect(reservation2.status).toBe('cancelled');
    expect(stock1.reserved).toBe(3);
    expect(stock2.reserved).toBe(1);
    expect(mockAmqp.publish).toHaveBeenCalledWith(
      'catalog_item_stock.exchange',
      'catalog_item_stock.cancel.success',
      items
    );
  });

  it('should throw error if trying to cancel more than reserved', async () => {
    const items = [{ itemId: 1, amount: 10, basketId: 1 }];
    const stock = { id: 1, itemId: 1, total: 10, reserved: 5 };
    
    mockManager.findOne.mockResolvedValueOnce(stock);
    mockManager.find.mockResolvedValueOnce([]);

    await expect(service.cancelAtomic(items)).rejects.toThrow(/Insufficient reserved stock|Cannot cancel more than reserved|No active reservation found/);
  });

  it('should throw error if no active reservation found', async () => {
    const items = [{ itemId: 1, amount: 5, basketId: 1 }];
    const stock = { id: 1, itemId: 1, total: 10, reserved: 5 };
    
    mockManager.findOne.mockResolvedValueOnce(stock);
    mockManager.find.mockResolvedValueOnce([]);

    await expect(service.cancelAtomic(items)).rejects.toThrow('No active reservation found');
  });

  describe('checkActiveReservations', () => {
    it('should return success when all reservations are active', async () => {
      const items = [
        { itemId: 1, amount: 3, basketId: 1 },
        { itemId: 2, amount: 2, basketId: 1 },
      ];
      
      const reservation1 = { id: 1, itemId: 1, basketId: 1, amount: 3, status: 'reserved', expiresAt: new Date(Date.now() + 60000) };
      const reservation2 = { id: 2, itemId: 2, basketId: 1, amount: 2, status: 'reserved', expiresAt: new Date(Date.now() + 60000) };
      
      mockManager.find.mockResolvedValueOnce([reservation1, reservation2]);

      const result = await service.checkActiveReservations(items);

      expect(result.success).toBe(true);
      expect(result.missingItems).toEqual([]);
    });

    it('should return missing items when reservations are insufficient', async () => {
      const items = [
        { itemId: 1, amount: 5, basketId: 1 },
        { itemId: 2, amount: 2, basketId: 1 },
      ];
      
      const reservation1 = { id: 1, itemId: 1, basketId: 1, amount: 3, status: 'reserved', expiresAt: new Date(Date.now() + 60000) };
      const reservation2 = { id: 2, itemId: 2, basketId: 1, amount: 2, status: 'reserved', expiresAt: new Date(Date.now() + 60000) };
      
      mockManager.find.mockResolvedValueOnce([reservation1, reservation2]);

      const result = await service.checkActiveReservations(items);

      expect(result.success).toBe(false);
      expect(result.missingItems).toEqual([1]);
    });

    it('should return success for empty items array', async () => {
      const result = await service.checkActiveReservations([]);

      expect(result.success).toBe(true);
      expect(result.missingItems).toEqual([]);
    });

    it('should throw error if items have different basketIds', async () => {
      const items = [
        { itemId: 1, amount: 3, basketId: 1 },
        { itemId: 2, amount: 2, basketId: 2 },
      ];

      await expect(service.checkActiveReservations(items)).rejects.toThrow('All items must have the same basketId');
    });
  });
});
