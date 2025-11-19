using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Caching;

public class StockCache
{
    private readonly ConcurrentDictionary<int, RabbitMQFullDTOItem> _stocks = new();
    private readonly IRabbitMqService _rabbitMqService;
    public StockCache(IRabbitMqService rabbitMqService)
    {
        _rabbitMqService = rabbitMqService;
    }

    public async Task Initialize()
    {
        var items = await _rabbitMqService.GetFullStockAsync();

        foreach (var item in items)
            Update(item);
    }

    public void Update(int itemId, int total, int reserved)
    {
        _stocks[itemId] = new RabbitMQFullDTOItem(itemId, total, reserved);
    }

    public void Update(RabbitMQFullDTOItem stockItem)
    {
        _stocks[stockItem.itemId] = stockItem;
    }

    public RabbitMQFullDTOItem? Get(int itemId)
    {
        _stocks.TryGetValue(itemId, out var stock);
        return stock;
    }

    public IReadOnlyCollection<RabbitMQFullDTOItem> GetAll()
    {
        return _stocks.Values.ToArray();
    }
}


