using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.DTOs;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using static System.Net.WebRequestMethods;

namespace Microsoft.eShopWeb.Infrastructure.Clients;

public class CatalogApiClient : ICatalogApiClient
{
    private readonly HttpClient _httpClient;

    public CatalogApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<CatalogTypeDTO>> GetCatalogTypesAsync()
    {
        var response = await _httpClient.GetAsync("types");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<CatalogTypeDTO>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<CatalogTypeDTO>();
    }

    public async Task<List<CatalogBrandDTO>> GetBrandsAsync()
    {
        var response = await _httpClient.GetAsync("brands");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<List<CatalogBrandDTO>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result ?? new List<CatalogBrandDTO>();
    }

    public async Task<ListPagedCatalogItemResponse> GetCatalogItemsAsync()
    {
        var response = await _httpClient.GetAsync("items");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ListPagedCatalogItemResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new ListPagedCatalogItemResponse();
    }

    public async Task<ListPagedCatalogItemResponse> GetCatalogItemsAsync(
        int pageIndex, int pageSize, int? brandId = null, int? typeId = null)
    {
        var url = $"items?pageSize={pageSize}&pageIndex={pageIndex}";

        if (brandId.HasValue) url += $"&catalogBrandId={brandId.Value}";
        if (typeId.HasValue) url += $"&catalogTypeId={typeId.Value}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ListPagedCatalogItemResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new ListPagedCatalogItemResponse();
    }

    public async Task<CatalogItemDTO> GetCatalogItemAsync(int catalogItemId)
    {
        var url = $"items/{catalogItemId}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CatalogItemDTO>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new CatalogItemDTO();
    }

    public async Task<CatalogItemDTO> UpdateCatalogItemAsync(CatalogItemDTO item)
    {
        var response = await _httpClient.PutAsJsonAsync("items", item);

        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<CatalogItemDTO>();
        return updated!;
    }
}
