using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.DTOs.Basket;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.eShopWeb.Infrastructure.Http.Clients;

public class BasketClient : IBasketClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BasketClient> _logger;
    
    public BasketClient(HttpClient httpClient, IConfiguration configuration, ILogger<BasketClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(configuration["baseUrls:basketMicroservice"].TrimEnd('/') + "/");
        _logger = logger;
    }

    public BasketClient(HttpClient httpClient, ILogger<BasketClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<BasketClient>.Instance;
    }

    public async Task<BasketDTO?> GetBasket(int basketId)
    {
        _logger.LogInformation("Retrieving basket {BasketId}.", basketId);
        var response = await _httpClient.GetFromJsonAsync<BasketDTO>($"{basketId}");

        if (response == null)
        {
            _logger.LogWarning("Basket {BasketId} not found.", basketId);
        }
        else
        {
            _logger.LogInformation("Retrieved basket {BasketId} with {ItemCount} items.", basketId, response.Items.Count);
        }
        return response;
    }
    
    public async Task<BasketDTO> GetOrCreateBasketByBuyerId(string username)
    {
        _logger.LogInformation("Getting or creating basket for buyer {BuyerId}.", username);
        var result= await _httpClient.GetFromJsonAsync<BasketDTO>($"getOrCreate/{username}");
        _logger.LogInformation("Basket {BasketId} retrieved for buyer {BuyerId}.", result?.Id, username);
        return result;
    }

    public Task<int> CountTotalBasketItems(string username)
    {
        _logger.LogInformation("Counting basket items for buyer {BuyerId}.", username);
        var result = _httpClient.GetFromJsonAsync<int>($"count/{username}");

        return result;
    }

    public async Task<BasketDTO> SetQuantities(int basketId, Dictionary<string, int> quantities)
    {
        var dto = new UpdateQuantitiesDto(basketId, quantities);

        _logger.LogInformation("Setting quantities for basket {BasketId}.", basketId);
        var response = await _httpClient.PatchAsJsonAsync("setQuantities", dto);

        var result = await response.Content.ReadFromJsonAsync<BasketDTO>();
        _logger.LogInformation("Updated quantities for basket {BasketId}.", basketId);
        return result;
    }

    public Task<BasketDTO> AddItemToBasket(AddBasketItemDto addBasketItemDto)
    {
        _logger.LogInformation("Adding catalog item {CatalogItemId} to basket {BasketId}.", addBasketItemDto.CatalogItemId, addBasketItemDto.BasketId);
        var result = _httpClient.PostAsJsonAsync("addItem", addBasketItemDto)
            .Result.Content.ReadFromJsonAsync<BasketDTO>();

        return result;
    }
    
    public async Task TransferBasketAsync(string anonymousId, string userName)
    {
        _logger.LogInformation("Transferring basket from anonymous {AnonymousId} to user {UserName}.", anonymousId, userName);
        var dto = new TransferDTO(anonymousId, userName);

        var response = await _httpClient.PutAsJsonAsync("transfer", dto);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to transfer basket from {AnonymousId} to {UserName}. StatusCode: {StatusCode}", anonymousId, userName, response.StatusCode);
            throw new Exception("Error transferring basket");
        }
        _logger.LogInformation("Successfully transferred basket from {AnonymousId} to {UserName}.", anonymousId, userName);
    }
    
    public async Task DeleteBasketAsync(int basketId)
    {
        _logger.LogInformation("Deleting basket {BasketId}.", basketId);
        var response = await _httpClient.DeleteAsync(basketId.ToString());

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to delete basket {BasketId}. StatusCode: {StatusCode}", basketId, response.StatusCode);
            throw new Exception("Error deleting basket");
        }
        _logger.LogInformation("Basket {BasketId} deleted.", basketId);
    }
}
