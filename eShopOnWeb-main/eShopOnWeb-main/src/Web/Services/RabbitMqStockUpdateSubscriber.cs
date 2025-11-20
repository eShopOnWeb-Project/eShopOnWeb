using System.Text;
using System.Text.Json;
using BlazorShared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.eShopWeb.Infrastructure.Caching;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;
using Microsoft.eShopWeb.Web.Hubs;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Microsoft.eShopWeb.Web.Services;

public class RabbitMqStockUpdateSubscriber : BackgroundService
{
    private readonly IHubContext<StockHub> _hub;
    private readonly ConnectionFactory _factory;
    private readonly StockCache _cache;
    private readonly ILogger<RabbitMqStockUpdateSubscriber> _logger;

    public RabbitMqStockUpdateSubscriber(IHubContext<StockHub> hub, IOptions<RabbitMqOptions> options, StockCache cache, ILogger<RabbitMqStockUpdateSubscriber> logger)
    {
        _hub = hub;
        _cache = cache;

        _factory = new ConnectionFactory
        {
            HostName = options.Value.HostName,
            UserName = options.Value.UserName,
            Password = options.Value.Password,
            Port = options.Value.Port
        };
        _logger = logger;

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await _factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        var queue = await channel.QueueDeclareAsync("", exclusive: true);

        var routingKeys = new[]
        {
            "catalog_item_stock.restock.success",
            "catalog_item_stock.reserve.success",
            "catalog_item_stock.cancel.success",
            "catalog_item_stock.confirm.success",
            "catalog_item_stock.reservation.expired"
        };

        foreach (var key in routingKeys)
        {
            await channel.QueueBindAsync(queue.QueueName, "catalog_item_stock.exchange", key);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                var routingKey = ea.RoutingKey;
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var items = JsonSerializer.Deserialize<List<RabbitMQDefaultDTOItem>>(json, options);

                if (items != null)
                {
                    foreach (var item in items)
                        await HandleMessageAsync(item, routingKey);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling RabbitMQ message: {ex}");
            }
        };

        await channel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(RabbitMQDefaultDTOItem msg, string routingKey)
    {
        var stock = _cache.Get(msg.itemId) ?? new RabbitMQFullDTOItem(msg.itemId, 0, 0);

        _logger.LogInformation("Handling stock update for ItemId: {ItemId}, RoutingKey: {RoutingKey}, Current Stock: Total={Total}, Reserved={Reserved}, Amount={Amount}",
            msg.itemId, routingKey, stock.total, stock.reserved, msg.amount);

        RabbitMQFullDTOItem updatedStock = routingKey switch
        {
            "catalog_item_stock.restock.success" => stock with { total = msg.amount },
            "catalog_item_stock.reserve.success" => stock with { reserved = msg.amount },
            "catalog_item_stock.cancel.success" => stock with { reserved = stock.reserved - msg.amount },
            "catalog_item_stock.confirm.success" => stock with
            {
                reserved = stock.reserved - msg.amount,
                total = stock.total - msg.amount
            },
            "catalog_item_stock.reservation.expired" => stock with { reserved = stock.reserved - msg.amount },
            _ => stock
        };

        if (updatedStock.reserved < 0)
            updatedStock = updatedStock with { reserved = 0 };

        _cache.Update(msg.itemId, updatedStock.total, updatedStock.reserved);

        _logger.LogInformation("Updated stock for ItemId: {ItemId}, New Stock: Total={Total}, Reserved={Reserved}",
            msg.itemId, updatedStock.total, updatedStock.reserved);

        await _hub.Clients.All.SendAsync("StockUpdated", updatedStock);
    }
}
