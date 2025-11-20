using Microsoft.eShopWeb.ApplicationCore.DTOs.Basket;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.eShopWeb.Web.Pages.Basket;
using Microsoft.eShopWeb.Web.Pages.Shared.Components.BasketComponent;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Web.Services;

public class BasketViewModelService : IBasketViewModelService
{
    private readonly IBasketClient _basketClient;
    private readonly IUriComposer _uriComposer;
    private readonly ICatalogApiClient _catalogApiClient;
    private readonly ILogger<BasketViewModelService> _logger;

    public BasketViewModelService(ICatalogApiClient catalogApiClient,
        IUriComposer uriComposer, IBasketClient basketClient, ILogger<BasketViewModelService> logger)
    {
        _uriComposer = uriComposer;
        _basketClient = basketClient;
        _catalogApiClient = catalogApiClient;
        _logger = logger;
    }

    public async Task<BasketViewModel> GetOrCreateBasketForUser(string userName)
    {
        _logger.LogInformation("Fetching basket for user {UserName}.", userName);
        var basket = await _basketClient.GetOrCreateBasketByBuyerId(userName);
        var viewModel = await Map(basket);
        _logger.LogInformation("Basket {BasketId} loaded for user {UserName}.", viewModel.Id, userName);
        return viewModel;
    }

    private async Task<List<BasketItemViewModel>> GetBasketItems(IReadOnlyCollection<BasketItemDTO> basketItems)
    {
        _logger.LogInformation("Mapping {ItemCount} basket items.", basketItems.Count);
        var catalogItemsResponse = await _catalogApiClient.GetCatalogItemsAsync();
        var catalogItems = catalogItemsResponse.CatalogItems;
        var basketItemIds = basketItems.Select(i => i.CatalogItemId).ToHashSet();
        catalogItems = catalogItems
            .Where(ci => basketItemIds.Contains(ci.Id))
            .ToList();

        var items = basketItems.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);

            var basketItemViewModel = new BasketItemViewModel
            {
                Id = basketItem.Id,
                UnitPrice = basketItem.UnitPrice,
                Quantity = basketItem.Quantity,
                CatalogItemId = basketItem.CatalogItemId,
                PictureUrl = _uriComposer.ComposePicUri(catalogItem.PictureUri),
                ProductName = catalogItem.Name
            };
            return basketItemViewModel;
        }).ToList();

        return items;
    }

    public async Task<BasketViewModel> Map(BasketDTO basket)
    {
        _logger.LogDebug("Creating BasketViewModel for basket {BasketId}.", basket.Id);
        return new BasketViewModel
        {
            BuyerId = basket.BuyerId, Id = basket.Id, Items = await GetBasketItems(basket.Items)
        };
    }

    public async Task<int> CountTotalBasketItems(string username)
    {
        _logger.LogDebug("Counting total basket items for user {UserName}.", username);
        var counter = await _basketClient.CountTotalBasketItems(username);

        _logger.LogDebug("User {UserName} has {Count} items in basket.", username, counter);
        return counter;
    }
}
