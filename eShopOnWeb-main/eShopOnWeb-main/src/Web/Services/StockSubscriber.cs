using System.Text;
using System.Text.Json;
using BlazorShared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.eShopWeb.ApplicationCore.DTOs.RabbitMQ;
using Microsoft.eShopWeb.Infrastructure.Caching;
using Microsoft.eShopWeb.Web.Hubs;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Microsoft.eShopWeb.Web.Services;

public class StockSubscriber : BackgroundService
{
    private readonly IHubContext<StockHub> _hub;
    private readonly ConnectionFactory _factory;
    private readonly StockCache _cache;

    public StockSubscriber(IHubContext<StockHub> hub, IOptions<RabbitMqOptions> options, StockCache cache)
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
            "catalog_item_stock.confirm.success"
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
                var items = JsonSerializer.Deserialize<List<Item>>(json, options);

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

    private async Task HandleMessageAsync(Item msg, string routingKey)
    {
        var stock = _cache.Get(msg.itemId) ?? new StockItem(msg.itemId, 0, 0);

        StockItem updatedStock = routingKey switch
        {
            "catalog_item_stock.restock.success" => stock with { Total = stock.Total + msg.amount },
            "catalog_item_stock.reserve.success" => stock with { Reserved = stock.Reserved + msg.amount },
            "catalog_item_stock.cancel.success"  => stock with { Reserved = stock.Reserved - msg.amount },
            "catalog_item_stock.confirm.success" => stock with
            {
                Reserved = stock.Reserved - msg.amount,
                Total = stock.Total - msg.amount
            },
            _ => stock
        };

        _cache.Update(msg.itemId, updatedStock.Total, updatedStock.Reserved);

        await _hub.Clients.All.SendAsync("StockUpdated", updatedStock);
    }
}
