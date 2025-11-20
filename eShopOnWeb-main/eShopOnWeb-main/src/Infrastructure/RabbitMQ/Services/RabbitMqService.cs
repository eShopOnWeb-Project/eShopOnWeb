using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.Interfaces;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Microsoft.eShopWeb.Infrastructure.RabbitMQ.Services;

public class RabbitMqService : IRabbitMqService
{
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqService> _logger;

    public RabbitMqService(RabbitMqOptions options, ILogger<RabbitMqService> logger)
    {
        _factory = new ConnectionFactory
        {
            HostName = options.HostName,
            UserName = options.UserName,
            Password = options.Password,
            Port = options.Port
        };
        _logger = logger;
    }

    public async Task<RabbitMQReserveResponse> ReserveItemAsync(int itemId, int amount)
    {
        _logger.LogInformation("Reserving single item {ItemId} with amount {Amount}.", itemId, amount);
        return await ReserveAsync(new List<RabbitMQDefaultDTOItem> { new() { itemId = itemId, amount = amount } });
    }

    public async Task<RabbitMQReserveResponse> ReserveAsync(List<RabbitMQDefaultDTOItem> items)
    {
        _logger.LogInformation("Publishing reservation request for {ItemCount} items.", items.Count);
        await using var connection = await _factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var replyQueue = await channel.QueueDeclareAsync(queue: "", exclusive: true);

        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<RabbitMQReserveResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            if (ea.BasicProperties.CorrelationId == correlationId)
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var response = JsonSerializer.Deserialize<RabbitMQReserveResponse>(json);
                tcs.TrySetResult(response!);
            }
            await Task.Yield();
        };

        await channel.BasicConsumeAsync(
            queue: replyQueue.QueueName,
            autoAck: true,
            consumer: consumer
        );

        var messageBody = JsonSerializer.Serialize(items);
        var body = Encoding.UTF8.GetBytes(messageBody);

        var props = new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = replyQueue.QueueName
        };

        await channel.BasicPublishAsync(
            exchange: "catalog_item_stock.exchange",
            routingKey: "catalog_item_stock.reserve",
            mandatory: true,
            basicProperties: props,
            body: body
        );

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        if (completedTask != tcs.Task)
        {
            _logger.LogWarning("Reservation request timed out.");
            throw new TimeoutException("RPC request timed out waiting for response");
        }

        var response = await tcs.Task;
        _logger.LogInformation("Reservation response received. Success={Success}, Reason={Reason}.", response.success, response.reason);
        return response;
    }

    public async Task<List<RabbitMQFullDTOItem>> GetFullStockAsync()
    {
        _logger.LogInformation("Requesting full stock snapshot from RabbitMQ.");
        await using var connection = await _factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var replyQueue = await channel.QueueDeclareAsync("", exclusive: true);
        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<List<RabbitMQFullDTOItem>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            if (ea.BasicProperties.CorrelationId == correlationId)
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var stock = JsonSerializer.Deserialize<List<RabbitMQFullDTOItem>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                tcs.TrySetResult(stock!);
            }
            await Task.Yield();
        };

        await channel.BasicConsumeAsync(replyQueue.QueueName, autoAck: true, consumer: consumer);

        var props = new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = replyQueue.QueueName
        };

        var body = Encoding.UTF8.GetBytes("{}");
        await channel.BasicPublishAsync(
            exchange: "catalog_item_stock.exchange",
            routingKey: "catalog_item_stock.getall",
            mandatory: true,
            basicProperties: props,
            body: body
        );

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        if (completedTask != tcs.Task)
        {
            _logger.LogWarning("Full stock snapshot request timed out.");
            throw new TimeoutException("RPC request timed out waiting for response");
        }

        var stock = await tcs.Task;
        _logger.LogInformation("Received full stock snapshot with {Count} items.", stock.Count);
        return stock;
    }

    public async Task<CheckActiveReservationsResponse> CheckActiveReservationsAsync(List<RabbitMQDefaultDTOItem> items)
    {
        _logger.LogInformation("Checking active reservations for {ItemCount} items.", items.Count);
        await using var connection = await _factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var replyQueue = await channel.QueueDeclareAsync("", exclusive: true);
        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<CheckActiveReservationsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            if (ea.BasicProperties.CorrelationId == correlationId)
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var response = JsonSerializer.Deserialize<CheckActiveReservationsResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                tcs.TrySetResult(response!);
            }
            await Task.Yield();
        };

        await channel.BasicConsumeAsync(replyQueue.QueueName, autoAck: true, consumer: consumer);

        var messageBody = JsonSerializer.Serialize(items);
        var body = Encoding.UTF8.GetBytes(messageBody);

        var props = new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = replyQueue.QueueName
        };

        await channel.BasicPublishAsync(
            exchange: "catalog_item_stock.exchange",
            routingKey: "catalog_item_stock.check_active_reservations",
            mandatory: true,
            basicProperties: props,
            body: body
        );

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        if (completedTask != tcs.Task)
        {
            _logger.LogWarning("Active reservations check timed out.");
            throw new TimeoutException("RPC request timed out waiting for response");
        }

        var response = await tcs.Task;
        _logger.LogInformation("Active reservations response received. Success={Success}, MissingCount={MissingCount}.", response.success, response.missingItems.Count);
        return response;
    }

    public async Task SendRestockAsync(List<RabbitMQDefaultDTOItem> items)
    {
        _logger.LogInformation("Publishing restock event for {ItemCount} items.", items.Count);
        await PublishAsync("catalog_item_stock.restock", items);
    }

    public async Task SendCancelAsync(List<RabbitMQDefaultDTOItem> items)
    {
        _logger.LogInformation("Publishing cancel event for {ItemCount} items.", items.Count);
        await PublishAsync("catalog_item_stock.cancel", items);
    }

    private async Task PublishAsync(string routingKey, List<RabbitMQDefaultDTOItem> items)
    {
        _logger.LogInformation("Publishing RabbitMQ message with routing key {RoutingKey} for {ItemCount} items.", routingKey, items.Count);
        await using var connection = await _factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(items));

        var props = new BasicProperties
        {
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: "catalog_item_stock.exchange",
            routingKey: routingKey,
            true,
            basicProperties: props,
            body: body
        );
        _logger.LogInformation("Message with routing key {RoutingKey} published.", routingKey);
    }
}
