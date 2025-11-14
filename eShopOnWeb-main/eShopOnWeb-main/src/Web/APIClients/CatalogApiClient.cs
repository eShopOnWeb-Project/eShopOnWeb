using System.Text.Json;
using Microsoft.eShopWeb.Web.DTOs;
using Microsoft.eShopWeb.Web.ViewModels;
using static System.Net.WebRequestMethods;

namespace Microsoft.eShopWeb.Web.APIClients;

public interface ICatalogApiClient
{
    Task<ListPagedCatalogItemResponse> GetCatalogItemsAsync(int pageIndex, int pageSize, int? brandId = null, int? typeId = null);
    Task<List<CatalogBrandDTO>> GetBrandsAsync();
    Task<List<CatalogTypeDTO>> GetCatalogTypesAsync();
    Task<CatalogItemDTO> GetCatalogItemAsync(int catalogItemId);
    Task<CatalogItemDTO> UpdateCatalogItemAsync(CatalogItemDTO item);
}

public class CatalogApiClient : ICatalogApiClient
{
    private readonly HttpClient _httpClient;

    public CatalogApiClient(IHttpClientFactory factory)
    {
        _httpClient = factory.CreateClient("Gateway");
    }

    public async Task<List<CatalogTypeDTO>> GetCatalogTypesAsync()
    {
        var response = await _httpClient.GetAsync("/catalog/types");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<CatalogTypeDTO>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<CatalogTypeDTO>();
    }

    public async Task<List<CatalogBrandDTO>> GetBrandsAsync()
    {
        var response = await _httpClient.GetAsync("/catalog/brands");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<List<CatalogBrandDTO>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result ?? new List<CatalogBrandDTO>();
    }

    public async Task<ListPagedCatalogItemResponse> GetCatalogItemsAsync(
        int pageIndex, int pageSize, int? brandId = null, int? typeId = null)
    {
        var url = $"/catalog/items?pageSize={pageSize}&pageIndex={pageIndex}";

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
        var url = $"/catalog/items/{catalogItemId}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CatalogItemDTO>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new CatalogItemDTO();
    }

    public async Task<CatalogItemDTO> UpdateCatalogItemAsync(CatalogItemDTO item)
    {
        var response = await _httpClient.PutAsJsonAsync("/catalog/items", item);

        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<CatalogItemDTO>();
        return updated!;
    }
}
