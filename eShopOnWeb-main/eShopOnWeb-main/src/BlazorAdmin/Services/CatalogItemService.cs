using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorAdmin.Extensions;
using BlazorAdmin.Interfaces;
using BlazorAdmin.Models;
using BlazorShared.Models;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;


namespace BlazorAdmin.Services;

public class CatalogItemService : ICatalogItemService
{
    private readonly ICatalogBrandService _brandService;
    private readonly ICatalogTypeService _typeService;
    private readonly HttpService _httpService;
    private readonly ILogger<CatalogItemService> _logger;
    private readonly IUriComposer _uriComposer;

    public CatalogItemService(ICatalogBrandService brandService,
        ICatalogTypeService typeService,
        HttpService httpService,
        ILogger<CatalogItemService> logger,
        IUriComposer uriComposer)
    {
        _brandService = brandService;
        _typeService = typeService;
        _httpService = httpService;
        _logger = logger;
        _uriComposer = uriComposer;
    }

    public async Task<CatalogItem> Create(CreateCatalogItemRequest catalogItem)
    {
        _logger.LogInformation("Creating catalog item {ItemName}.", catalogItem.Name);
        var result = await _httpService.HttpPost<CatalogItemDTO>("items", catalogItem.AsDTO).ToCatalogItemAsync(_uriComposer);
        _logger.LogInformation("Catalog item {ItemId} created.", result.Id);
        return result;
    }

    public async Task<CatalogItem> Edit(CatalogItem catalogItem)
    {
        _logger.LogInformation("Updating catalog item {ItemId}.", catalogItem.Id);
        var result = await _httpService.HttpPut<CatalogItemDTO>("items", catalogItem.AsDTO).ToCatalogItemAsync(_uriComposer);
        _logger.LogInformation("Catalog item {ItemId} updated.", result.Id);
        return result;
    }

    public async Task<string> Delete(int catalogItemId)
    {
        _logger.LogInformation("Deleting catalog item {ItemId}.", catalogItemId);
        var status = (await _httpService.HttpDelete<DeleteCatalogItemResponse>("items", catalogItemId)).Status;
        _logger.LogInformation("Catalog item {ItemId} delete status: {Status}.", catalogItemId, status);
        return status;
    }

    public async Task<CatalogItem> GetById(int id)
    {
        var brandListTask = _brandService.List();
        var typeListTask = _typeService.List();
        var itemGetTask = _httpService.HttpGet<CatalogItemDTO>($"items/{id}").ToCatalogItemAsync(_uriComposer);
        await Task.WhenAll(brandListTask, typeListTask, itemGetTask);
        var brands = brandListTask.Result;
        var types = typeListTask.Result;
        var catalogItem = itemGetTask.Result;
        catalogItem.CatalogBrand = brands.FirstOrDefault(b => b.Id == catalogItem.CatalogBrandId)?.Name;
        catalogItem.CatalogType = types.FirstOrDefault(t => t.Id == catalogItem.CatalogTypeId)?.Name;
        return catalogItem;
    }

    public async Task<List<CatalogItem>> ListPaged(int pageSize)
    {
        _logger.LogInformation("Fetching catalog items from API.");

        var brandListTask = _brandService.List();
        var typeListTask = _typeService.List();
        var itemListTask = _httpService.HttpGet<ListPagedCatalogItemResponse>($"items?pageSize={pageSize}").ToCatalogItemListAsync(_uriComposer);
        await Task.WhenAll(brandListTask, typeListTask, itemListTask);

        var brands = brandListTask.Result;
        var types = typeListTask.Result;
        var items = itemListTask.Result;

        foreach (var item in items)
        {
            item.CatalogBrand = brands.FirstOrDefault(b => b.Id == item.CatalogBrandId)?.Name;
            item.CatalogType = types.FirstOrDefault(t => t.Id == item.CatalogTypeId)?.Name;
        }
        return items;
    }

    public async Task<List<CatalogItem>> List()
    {
        _logger.LogInformation("Fetching catalog items from API.");

        var brandListTask = _brandService.List();
        var typeListTask = _typeService.List();
        var itemListTask = _httpService.HttpGet<ListPagedCatalogItemResponse>($"items").ToCatalogItemListAsync(_uriComposer);
        await Task.WhenAll(brandListTask, typeListTask, itemListTask);

        var brands = brandListTask.Result;
        var types = typeListTask.Result;
        var items = itemListTask.Result;

        foreach (var item in items)
        {
            item.CatalogBrand = brands.FirstOrDefault(b => b.Id == item.CatalogBrandId)?.Name;
            item.CatalogType = types.FirstOrDefault(t => t.Id == item.CatalogTypeId)?.Name;
        }
        return items;
    }
}
