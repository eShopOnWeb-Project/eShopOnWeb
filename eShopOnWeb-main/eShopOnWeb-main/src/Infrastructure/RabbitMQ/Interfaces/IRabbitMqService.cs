using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;


namespace Microsoft.eShopWeb.Infrastructure.RabbitMQ.Interfaces;
public interface IRabbitMqService
{
    Task<RabbitMQReserveResponse> ReserveItemAsync(int itemId, int amount);
    Task<RabbitMQReserveResponse> ReserveAsync(List<RabbitMQDefaultDTOItem> items);
    Task SendCancelAsync(List<RabbitMQDefaultDTOItem> items);
    Task SendRestockAsync(List<RabbitMQDefaultDTOItem> items);
    Task<List<RabbitMQFullDTOItem>> GetFullStockAsync();
}
