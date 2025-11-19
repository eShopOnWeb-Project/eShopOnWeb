using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.DTOs.Basket;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Microsoft.eShopWeb.Infrastructure.Http.Clients;

public class BasketClient : IBasketClient
{
    private readonly HttpClient _httpClient;
    
    public BasketClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(configuration["baseUrls:basketMicroservice"].TrimEnd('/') + "/");
    }

    public BasketClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BasketDTO?> GetBasket(int basketId)
    {
        var response = await _httpClient.GetFromJsonAsync<BasketDTO>($"{basketId}");
        
        return response;
    }
    
    public async Task<BasketDTO> GetOrCreateBasketByBuyerId(string username)
    {
        var result= await _httpClient.GetFromJsonAsync<BasketDTO>($"getOrCreate/{username}");

        return result;
    }

    public Task<int> CountTotalBasketItems(string username)
    {
        var result = _httpClient.GetFromJsonAsync<int>($"count/{username}");

        return result;
    }

    public async Task<BasketDTO> SetQuantities(int basketId, Dictionary<string, int> quantities)
    {
        var dto = new UpdateQuantitiesDto(basketId, quantities);

        var response = await _httpClient.PatchAsJsonAsync("setQuantities", dto);

        var result = await response.Content.ReadFromJsonAsync<BasketDTO>();
        return result;
    }

    public Task<BasketDTO> AddItemToBasket(AddBasketItemDto addBasketItemDto)
    {
        var result = _httpClient.PostAsJsonAsync("addItem", addBasketItemDto)
            .Result.Content.ReadFromJsonAsync<BasketDTO>();

        return result;
    }
    
    public async Task TransferBasketAsync(string anonymousId, string userName)
    {
        var dto = new TransferDTO(anonymousId, userName);

        var response = await _httpClient.PutAsJsonAsync("transfer", dto);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Error transferring basket");
        }
    }
    
    public async Task DeleteBasketAsync(int basketId)
    {
        var response = await _httpClient.DeleteAsync(basketId.ToString());

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Error deleting basket");
        }
    }
}
