import { Injectable } from '@nestjs/common';
import { RabbitSubscribe, RabbitRPC } from '@golevelup/nestjs-rabbitmq';
import { CatalogItemStockService } from './catalog-item-stock.service';

export interface DefaultDTOItem {
  itemId: number;
  amount: number;
}

export interface FullDTOItem {
  itemId: number;
  total: number;
  reserved: number;
}

@Injectable()
export class CatalogItemStockConsumer {
  constructor(private readonly stockService: CatalogItemStockService) {}

  @RabbitSubscribe({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.restock',
    queue: 'catalog_item_stock_restock_queue',
  })
  async handleRestock(msg: DefaultDTOItem[]) {
    try {
      console.log('Restock event received:', msg);
      await this.stockService.restockAtomic(msg);
      console.log('Restock batch success');
    } catch (err: any) {
      console.error(`Restock batch failed: ${err.message}`);
    }
  }

  @RabbitRPC({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.getall',
    queue: 'catalog_item_stock_getall_queue',
  })
  async handleGetAll(msg: any): Promise<FullDTOItem[]> {
    console.log('Get All Stock RPC request received');
    try {
      return await this.stockService.getFullStock();
    } catch (err: any) {
      console.error(`Get All Stock RPC request failed: ${err.message}`);
      return [];
    }
  }

  @RabbitRPC({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.reserve',
    queue: 'catalog_item_stock_reserve_rpc_queue',
  })
  async handleReserveRpc(msg: DefaultDTOItem[]) {
    console.log('Reserve RPC request received:', msg);
    try {
      await this.stockService.reserveAtomic(msg);
      return { success: true };
    } catch (err: any) {
      console.error(`Reserve batch failed: ${err.message}`);
      return { success: false, reason: err.message };
    }
  }

  @RabbitSubscribe({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.confirm',
    queue: 'catalog_item_stock_confirm_queue',
  })
  async handleConfirm(msg: DefaultDTOItem[]) {
    try {
      console.log('Confirm Order event received:', msg);
      await this.stockService.confirmAtomic(msg);
      console.log('Confirm batch success');
    } catch (err: any) {
      console.error(`Confirm batch failed: ${err.message}`);
    }
  }

  @RabbitSubscribe({
    exchange: 'catalog_item_stock.exchange',
    routingKey: 'catalog_item_stock.cancel',
    queue: 'catalog_item_stock_cancel_queue',
  })
  async handleCancel(msg: DefaultDTOItem[]) {
    try {
      console.log('Cancel Reservation event received:', msg);
      await this.stockService.cancelAtomic(msg);
      console.log('Cancel batch success');
    } catch (err: any) {
      console.error(`Cancel batch failed: ${err.message}`);
    }
  }
}
