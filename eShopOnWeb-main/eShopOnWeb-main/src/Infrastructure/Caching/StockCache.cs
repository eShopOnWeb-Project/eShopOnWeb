using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.Interfaces;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Caching;

public class StockCache
{
    private readonly ConcurrentDictionary<int, RabbitMQFullDTOItem> _stocks = new();
    private readonly IRabbitMqService _rabbitMqService;
    private readonly ILogger<StockCache> _logger;
    public StockCache(IRabbitMqService rabbitMqService, ILogger<StockCache> logger)
    {
        _rabbitMqService = rabbitMqService;
        _logger = logger;
    }

    public async Task Initialize()
    {
        _logger.LogInformation("Initializing stock cache from RabbitMQ service.");
        var items = await _rabbitMqService.GetFullStockAsync();

        foreach (var item in items)
            Update(item);
        _logger.LogInformation("Stock cache initialized with {Count} items.", items.Count);
    }

    public void Update(int itemId, int total, int reserved)
    {
        _stocks[itemId] = new RabbitMQFullDTOItem(itemId, total, reserved);
        _logger.LogInformation("Stock cache updated for ItemId {ItemId}: Total={Total}, Reserved={Reserved}.", itemId, total, reserved);
    }

    public void Update(RabbitMQFullDTOItem stockItem)
    {
        _stocks[stockItem.itemId] = stockItem;
        _logger.LogInformation("Stock cache updated for ItemId {ItemId}: Total={Total}, Reserved={Reserved}.", stockItem.itemId, stockItem.total, stockItem.reserved);
    }

    public RabbitMQFullDTOItem? Get(int itemId)
    {
        _stocks.TryGetValue(itemId, out var stock);
        if (stock == null)
        {
            _logger.LogWarning("Stock cache miss for ItemId {ItemId}.", itemId);
        }
        else
        {
            _logger.LogDebug("Stock cache hit for ItemId {ItemId}.", itemId);
        }
        return stock;
    }

    public IReadOnlyCollection<RabbitMQFullDTOItem> GetAll()
    {
        _logger.LogInformation("Retrieving all stock cache entries ({Count}).", _stocks.Count);
        return _stocks.Values.ToArray();
    }
}


