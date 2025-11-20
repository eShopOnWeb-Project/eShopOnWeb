using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.DTOs;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Http.Clients;

public class CatalogApiClient : ICatalogApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogApiClient> _logger;

    public CatalogApiClient(IHttpClientFactory factory, ILogger<CatalogApiClient> logger)
    {
        _httpClient = factory.CreateClient("Gateway");
        _logger = logger;
    }

    public async Task<List<CatalogTypeDTO>> GetCatalogTypesAsync()
    {
        _logger.LogInformation("Requesting catalog types from catalog service.");
        var response = await _httpClient.GetAsync("/catalog/types");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<List<CatalogTypeDTO>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<CatalogTypeDTO>();

        _logger.LogInformation("Retrieved {Count} catalog types.", result.Count);
        return result;
    }

    public async Task<List<CatalogBrandDTO>> GetBrandsAsync()
    {
        _logger.LogInformation("Requesting catalog brands from catalog service.");
        var response = await _httpClient.GetAsync("/catalog/brands");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<List<CatalogBrandDTO>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var brands = result ?? new List<CatalogBrandDTO>();
        _logger.LogInformation("Retrieved {Count} catalog brands.", brands.Count);
        return brands;
    }

    public async Task<ListPagedCatalogItemResponse> GetCatalogItemsAsync()
    {
        _logger.LogInformation("Requesting catalog items using default paging.");
        var response = await _httpClient.GetAsync("/catalog/items");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ListPagedCatalogItemResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new ListPagedCatalogItemResponse();

        _logger.LogInformation("Retrieved {Count} catalog items.", result.CatalogItems.Count);
        return result;
    }

    public async Task<ListPagedCatalogItemResponse> GetCatalogItemsAsync(
        int pageIndex, int pageSize, int? brandId = null, int? typeId = null)
    {
        var url = $"/catalog/items?pageSize={pageSize}&pageIndex={pageIndex}";

        if (brandId.HasValue) url += $"&catalogBrandId={brandId.Value}";
        if (typeId.HasValue) url += $"&catalogTypeId={typeId.Value}";

        _logger.LogInformation("Requesting catalog items page {PageIndex} with size {PageSize}. Brand={BrandId}, Type={TypeId}", pageIndex, pageSize, brandId, typeId);

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ListPagedCatalogItemResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new ListPagedCatalogItemResponse();

        _logger.LogInformation("Received {Count} catalog items for page {PageIndex}.", result.CatalogItems.Count, pageIndex);
        return result;
    }

    public async Task<CatalogItemDTO> GetCatalogItemAsync(int catalogItemId)
    {
        var url = $"/catalog/items/{catalogItemId}";

        _logger.LogInformation("Requesting catalog item {CatalogItemId}.", catalogItemId);
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CatalogItemDTO>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new CatalogItemDTO();

        _logger.LogInformation("Received catalog item {CatalogItemId}.", catalogItemId);
        return result;
    }

    public async Task<CatalogItemDTO> UpdateCatalogItemAsync(CatalogItemDTO item)
    {
        _logger.LogInformation("Updating catalog item {CatalogItemId}.", item.Id);
        var response = await _httpClient.PutAsJsonAsync("/catalog/items", item);

        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<CatalogItemDTO>();
        _logger.LogInformation("Catalog item {CatalogItemId} updated.", item.Id);
        return updated!;
    }
}
