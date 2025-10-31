using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using BlazorShared.Models;

namespace Microsoft.eShopWeb.Web.Cache;

public class StockCache
{
    private readonly ConcurrentDictionary<int, StockItem> _stocks = new();
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
        _stocks[itemId] = new StockItem (itemId, total, reserved);
    }

    public void Update(StockItem stockItem)
    {
        _stocks[stockItem.ItemId] = stockItem;
    }

    public StockItem? Get(int itemId)
    {
        _stocks.TryGetValue(itemId, out var stock);
        return stock;
    }

    public IReadOnlyCollection<StockItem> GetAll()
    {
        return _stocks.Values.ToArray();
    }
}


