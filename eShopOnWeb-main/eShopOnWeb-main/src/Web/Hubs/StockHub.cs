using BlazorShared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.eShopWeb.Web.Cache;
using Microsoft.eShopWeb.Web.Subscribers;
using static IRabbitMqService;

namespace Microsoft.eShopWeb.Web.Hubs;

public class StockHub : Hub
{
    private readonly IRabbitMqService _rabbitMqService;
    private readonly StockCache _cache;

    public StockHub(IRabbitMqService rabbitMqService, StockCache stockCache)
    {
        _rabbitMqService = rabbitMqService;
        _cache = stockCache;
    }

    public async Task Restock(int itemId, int amount)
    {
        await _rabbitMqService.SendRestockAsync(new List<Item> { new() { itemId=itemId, amount=amount } });
    }

    public async Task<List<StockItem>> GetStockCacheAsync()
    {
        return _cache.GetAll().ToList();
    }
}
