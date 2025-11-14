using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.eShopWeb.ApplicationCore.DTOs.RabbitMQ;


namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;
public interface IRabbitMqService
{
    Task<ReserveResponse> ReserveItemAsync(int itemId, int amount);
    Task<ReserveResponse> ReserveAsync(List<Item> items);
    Task SendCancelAsync(List<Item> items);
    Task SendRestockAsync(List<Item> items);
    Task<List<StockItem>> GetFullStockAsync();
}
