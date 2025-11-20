import { Injectable, Logger } from '@nestjs/common';
import { RabbitSubscribe, RabbitRPC } from '@golevelup/nestjs-rabbitmq';
import { CatalogItemStockService } from './catalog-item-stock.service';
import { DefaultDTOItem } from './dto/default-dto-item.interface';
import { FullDTOItem } from './dto/full-dto-item.interface';

@Injectable()
export class CatalogItemStockConsumer {
  private readonly logger = new Logger(CatalogItemStockConsumer.name);

  constructor(private readonly stockService: CatalogItemStockService) {}

  @RabbitSubscribe({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.restock',
    queue: 'catalog_item_stock_restock_queue',
  })
  async handleRestock(msg: DefaultDTOItem[]) {
    const requestId = `restock-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    this.logger.log(`Restock event received [${requestId}] with ${msg?.length || 0} item(s)`);
    
    if (!msg || !Array.isArray(msg) || msg.length === 0) {
      this.logger.error(`Invalid restock message format [${requestId}]`, null, { message: msg });
      return;
    }

    try {
      await this.stockService.restockAtomic(msg);
      this.logger.log(`Successfully processed restock event [${requestId}] for ${msg.length} item(s)`);
    } catch (err: any) {
      const errorContext = {
        requestId,
        itemCount: msg?.length,
        errorCode: err.code || 'UNKNOWN',
        errorName: err.name,
        items: msg?.map(i => ({ itemId: i?.itemId, amount: i?.amount, basketId: i?.basketId })) || [],
      };
      
      if (err.code === 'INVALID_INPUT' || err.code === 'INSUFFICIENT_STOCK') {
        this.logger.warn(`Restock batch failed [${requestId}]: ${err.message}`, err.stack, errorContext);
      } else {
        this.logger.error(`Restock batch failed [${requestId}]: ${err.message}`, err.stack, errorContext);
      }
    }
  }

  @RabbitRPC({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.check_active_reservations',
    queue: 'catalog_item_stock_check_active_reservations_rpc_queue',
  })
  async handleCheckActiveReservations(msg: DefaultDTOItem[]): Promise<{ success: boolean; missingItems: number[] }> {
    const requestId = `check-reservations-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const basketId = msg?.[0]?.basketId;
    
    if (!msg || !Array.isArray(msg)) {
      this.logger.error(`Invalid check active reservations message format [${requestId}]`, null, { message: msg });
      return { success: false, missingItems: [] };
    }

    this.logger.debug(`Check active reservations RPC request received [${requestId}] for basketId ${basketId} with ${msg.length} item(s)`);
    
    try {
      const result = await this.stockService.checkActiveReservations(msg);
      this.logger.debug(`Check active reservations completed [${requestId}] for basketId ${basketId}: success=${result.success}, missingItems=${result.missingItems.length}`);
      return result;
    } catch (err: any) {
      const errorContext = {
        requestId,
        basketId,
        itemCount: msg?.length,
        errorCode: err.code || 'UNKNOWN',
        errorName: err.name,
      };
      this.logger.error(`Check active reservations failed [${requestId}] for basketId ${basketId}: ${err.message}`, err.stack, errorContext);
      return { success: false, missingItems: [] };
    }
  }

  @RabbitRPC({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.getall',
    queue: 'catalog_item_stock_getall_queue',
  })
  async handleGetAll(msg: any): Promise<FullDTOItem[]> {
    const requestId = `get-all-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    this.logger.debug(`Get All Stock RPC request received [${requestId}]`);
    
    try {
      const result = await this.stockService.getFullStock();
      this.logger.debug(`Get All Stock RPC request completed [${requestId}]: returned ${result.length} item(s)`);
      return result;
    } catch (err: any) {
      const errorContext = {
        requestId,
        errorCode: err.code || 'UNKNOWN',
        errorName: err.name,
      };
      this.logger.error(`Get All Stock RPC request failed [${requestId}]: ${err.message}`, err.stack, errorContext);
      return [];
    }
  }

  @RabbitRPC({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.reserve',
    queue: 'catalog_item_stock_reserve_rpc_queue',
  })
  async handleReserveRpc(msg: DefaultDTOItem[]): Promise<{ success: boolean; reason?: string; errorCode?: string }> {
    const requestId = `reserve-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const basketId = msg?.[0]?.basketId;
    
    if (!msg || !Array.isArray(msg) || msg.length === 0) {
      this.logger.error(`Invalid reserve message format [${requestId}]`, null, { message: msg });
      return { success: false, reason: 'Invalid message format', errorCode: 'INVALID_INPUT' };
    }

    this.logger.log(`Reserve RPC request received [${requestId}] for basketId ${basketId} with ${msg.length} item(s)`);
    
    try {
      await this.stockService.reserveAtomic(msg);
      this.logger.log(`Successfully processed reserve RPC request [${requestId}] for basketId ${basketId}`);
      return { success: true };
    } catch (err: any) {
      const errorContext = {
        requestId,
        basketId,
        itemCount: msg?.length,
        errorCode: err.code || 'UNKNOWN',
        errorName: err.name,
        items: msg?.map(i => ({ itemId: i?.itemId, amount: i?.amount })) || [],
      };
      
      const errorCode = err.code || 'UNKNOWN';
      const errorReason = err.message || 'Unknown error';
      
      if (err.code === 'INVALID_INPUT' || err.code === 'INSUFFICIENT_STOCK') {
        this.logger.warn(`Reserve batch failed [${requestId}] for basketId ${basketId}: ${errorReason}`, err.stack, errorContext);
      } else {
        this.logger.error(`Reserve batch failed [${requestId}] for basketId ${basketId}: ${errorReason}`, err.stack, errorContext);
      }
      
      return { success: false, reason: errorReason, errorCode };
    }
  }

  @RabbitSubscribe({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.confirm',
    queue: 'catalog_item_stock_confirm_queue',
  })
  async handleConfirm(msg: DefaultDTOItem[]) {
    const requestId = `confirm-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const basketId = msg?.[0]?.basketId;
    
    if (!msg || !Array.isArray(msg) || msg.length === 0) {
      this.logger.error(`Invalid confirm message format [${requestId}]`, null, { message: msg });
      return;
    }

    this.logger.log(`Confirm Order event received [${requestId}] for basketId ${basketId} with ${msg.length} item(s)`);
    
    try {
      await this.stockService.confirmAtomic(msg);
      this.logger.log(`Successfully processed confirm event [${requestId}] for basketId ${basketId}`);
    } catch (err: any) {
      const errorContext = {
        requestId,
        basketId,
        itemCount: msg?.length,
        errorCode: err.code || 'UNKNOWN',
        errorName: err.name,
        items: msg?.map(i => ({ itemId: i?.itemId, amount: i?.amount })) || [],
      };
      
      if (err.code === 'INSUFFICIENT_RESERVED_STOCK' || err.code === 'RESERVATION_MISMATCH' || err.code === 'INVALID_INPUT') {
        this.logger.warn(`Confirm batch failed [${requestId}] for basketId ${basketId}: ${err.message}`, err.stack, errorContext);
      } else {
        this.logger.error(`Confirm batch failed [${requestId}] for basketId ${basketId}: ${err.message}`, err.stack, errorContext);
      }
    }
  }

  @RabbitSubscribe({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.cancel',
    queue: 'catalog_item_stock_cancel_queue',
  })
  async handleCancel(msg: DefaultDTOItem[]) {
    const requestId = `cancel-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const basketId = msg?.[0]?.basketId;
    
    if (!msg || !Array.isArray(msg) || msg.length === 0) {
      this.logger.error(`Invalid cancel message format [${requestId}]`, null, { message: msg });
      return;
    }

    this.logger.log(`Cancel Reservation event received [${requestId}] for basketId ${basketId} with ${msg.length} item(s)`);
    
    try {
      await this.stockService.cancelAtomic(msg);
      this.logger.log(`Successfully processed cancel event [${requestId}] for basketId ${basketId}`);
    } catch (err: any) {
      const errorContext = {
        requestId,
        basketId,
        itemCount: msg?.length,
        errorCode: err.code || 'UNKNOWN',
        errorName: err.name,
        items: msg?.map(i => ({ itemId: i?.itemId, amount: i?.amount })) || [],
      };
      
      if (err.code === 'RESERVATION_NOT_FOUND' || err.code === 'INSUFFICIENT_RESERVED_STOCK' || err.code === 'RESERVATION_MISMATCH' || err.code === 'INVALID_INPUT') {
        this.logger.warn(`Cancel batch failed [${requestId}] for basketId ${basketId}: ${err.message}`, err.stack, errorContext);
      } else {
        this.logger.error(`Cancel batch failed [${requestId}] for basketId ${basketId}: ${err.message}`, err.stack, errorContext);
      }
    }
  }
}
