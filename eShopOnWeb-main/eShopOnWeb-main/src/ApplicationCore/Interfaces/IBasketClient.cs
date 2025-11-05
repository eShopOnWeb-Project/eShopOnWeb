using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.DTOs.Basket;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IBasketClient
{
    Task<BasketDTO?> GetBasket(int basketId);
    Task<BasketDTO> GetOrCreateBasketByBuyerId(string username);
    Task<int> CountTotalBasketItems(string username);
    Task<BasketDTO> SetQuantities(int basketId, Dictionary<string, int> quantities);
    Task<BasketDTO> AddItemToBasket(AddBasketItemDto addBasketItemDto);
    Task TransferBasketAsync(string anonymousId, string userName);
    Task DeleteBasketAsync(int basketId);
}
