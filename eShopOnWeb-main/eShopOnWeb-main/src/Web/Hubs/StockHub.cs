using BlazorShared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.eShopWeb.Infrastructure.Caching;
using Microsoft.eShopWeb.Web.Services;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.DTOs.RabbitMQ;

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
