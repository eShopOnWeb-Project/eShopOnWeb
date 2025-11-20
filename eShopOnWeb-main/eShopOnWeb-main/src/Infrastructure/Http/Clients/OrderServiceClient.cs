using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Contracts.Orders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Http.Clients;

public class OrderServiceClient : IOrderServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderServiceClient> _logger;


    public OrderServiceClient(IConfiguration configuration, IHttpClientFactory factory, ILogger<OrderServiceClient> logger)
    {
        var baseUrl = configuration["baseUrls:ordersMicroservice"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Missing configuration for baseUrls:ordersMicroservice");
        }

        if (!baseUrl.EndsWith("/")) baseUrl += "/";

        _httpClient = factory.CreateClient("Gateway");
        _httpClient.BaseAddress = new Uri(baseUrl);
        _logger = logger;
    }

    public async Task CreateOrderAsync(CreateOrderDto order)
    {
        _logger.LogInformation("Sending create order request for buyer {BuyerId}.", order.BuyerId);
        await _httpClient.PostAsJsonAsync("api/v1/orders", order);
        _logger.LogInformation("Create order request for buyer {BuyerId} completed.", order.BuyerId);
    }

    public async Task<OrderReadDto> GetOrderByIdAsync(int orderId)
    {
        _logger.LogInformation("Retrieving order {OrderId}.", orderId);
        var order = await _httpClient.GetFromJsonAsync<OrderReadDto>($"api/v1/orders/{orderId}");
        _logger.LogInformation("Retrieved order {OrderId}.", orderId);
        return order;
    }

    public async Task<List<OrderReadDto>> GetOrdersForUserAsync(string buyerId)
    {
        _logger.LogInformation("Retrieving orders for buyer {BuyerId}.", buyerId);
        var orders = await _httpClient.GetFromJsonAsync<List<OrderReadDto>>($"api/v1/orders?buyer_id={buyerId}");
        _logger.LogInformation("Retrieved {Count} orders for buyer {BuyerId}.", orders?.Count ?? 0, buyerId);
        return orders;
    }
}
