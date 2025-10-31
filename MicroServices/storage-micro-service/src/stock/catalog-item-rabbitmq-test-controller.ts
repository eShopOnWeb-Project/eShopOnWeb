import { Controller, Post, Body } from '@nestjs/common';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { IsInt, Min } from 'class-validator';
import { validateOrReject } from 'class-validator';

// --------------------------
// DTO for validating requests
// --------------------------
class TestStockDto {
  @IsInt()
  @Min(1)
  itemId: number;

  @IsInt()
  @Min(1)
  amount: number;
}

@Controller('test-stock')
export class TestStockController {
  constructor(private readonly amqpConnection: AmqpConnection) {}

  // -------------------------------
  // RESTOCK (async)
  // -------------------------------
  // Example request (POST /test-stock/restock):
  // {
  //   "itemId": 1,
  //   "amount": 5
  // }
  @Post('restock')
  async testRestock(@Body() body: TestStockDto) {
    try {
      await validateOrReject(body); // ensure valid input
      await this.amqpConnection.publish(
        'catalog_item_stock.exchange',
        'catalog_item_stock.restock',
        { itemId: body.itemId, amount: body.amount },
      );
      return { success: true, message: `Restock published for itemId=${body.itemId}, amount=${body.amount}` };
    } catch (err: any) {
      return { success: false, error: err.message };
    }
  }

  // -------------------------------
  // RESERVE (RPC, synchronous)
  // -------------------------------
  // Example request (POST /test-stock/reserve):
  // {
  //   "itemId": 1,
  //   "amount": 3
  // }
  @Post('reserve')
  async testReserve(@Body() body: TestStockDto) {
    try {
      await validateOrReject(body);
      const result = await this.amqpConnection.request({
        exchange: 'catalog_item_stock.exchange',
        routingKey: 'catalog_item_stock.reserve',
        payload: { itemId: body.itemId, amount: body.amount },
      });
      return { success: true, data: result };
    } catch (err: any) {
      return { success: false, error: err.message };
    }
  }

  // -------------------------------
  // CONFIRM (async)
  // -------------------------------
  // Example request (POST /test-stock/confirm):
  // {
  //   "itemId": 1,
  //   "amount": 2
  // }
  @Post('confirm')
  async testConfirm(@Body() body: TestStockDto) {
    try {
      await validateOrReject(body);
      await this.amqpConnection.publish(
        'catalog_item_stock.exchange',
        'catalog_item_stock.confirm',
        { itemId: body.itemId, amount: body.amount },
      );
      return { success: true, message: `Confirm published for itemId=${body.itemId}, amount=${body.amount}` };
    } catch (err: any) {
      return { success: false, error: err.message };
    }
  }

  // -------------------------------
  // CANCEL (async)
  // -------------------------------
  // Example request (POST /test-stock/cancel):
  // {
  //   "itemId": 1,
  //   "amount": 4
  // }
  @Post('cancel')
  async testCancel(@Body() body: TestStockDto) {
    try {
      await validateOrReject(body);
      await this.amqpConnection.publish(
        'catalog_item_stock.exchange',
        'catalog_item_stock.cancel',
        { itemId: body.itemId, amount: body.amount },
      );
      return { success: true, message: `Cancel published for itemId=${body.itemId}, amount=${body.amount}` };
    } catch (err: any) {
      return { success: false, error: err.message };
    }
  }

  // -------------------------------
  // TEST ALL ACTIONS
  // -------------------------------
  // Example request (POST /test-stock/all):
  // {
  //   "itemId": 1,
  //   "amount": 5
  // }
  // Publishes all 4 events: restock, reserve, confirm, cancel
  @Post('all')
  async testAll(@Body() body: TestStockDto) {
    try {
      await validateOrReject(body);
      // Restock
      await this.amqpConnection.publish(
        'catalog_item_stock.exchange',
        'catalog_item_stock.restock',
        { itemId: body.itemId, amount: body.amount },
      );
      // Reserve (RPC)
      const reserveResult = await this.amqpConnection.request({
        exchange: 'catalog_item_stock.exchange',
        routingKey: 'catalog_item_stock.reserve',
        payload: { itemId: body.itemId, amount: body.amount },
      });
      // Confirm
      await this.amqpConnection.publish(
        'catalog_item_stock.exchange',
        'catalog_item_stock.confirm',
        { itemId: body.itemId, amount: body.amount },
      );
      // Cancel
      await this.amqpConnection.publish(
        'catalog_item_stock.exchange',
        'catalog_item_stock.cancel',
        { itemId: body.itemId, amount: body.amount },
      );

      return {
        success: true,
        message: 'All stock events published successfully',
        reserveResult,
      };
    } catch (err: any) {
      return { success: false, error: err.message };
    }
  }
}