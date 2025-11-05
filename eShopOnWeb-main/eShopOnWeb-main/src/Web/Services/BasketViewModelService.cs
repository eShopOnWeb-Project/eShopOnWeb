using Microsoft.eShopWeb.ApplicationCore.DTOs.Basket;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.eShopWeb.Web.Pages.Basket;

namespace Microsoft.eShopWeb.Web.Services;

public class BasketViewModelService : IBasketViewModelService
{
    private readonly IBasketClient _basketClient;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<CatalogItem> _itemRepository;

    public BasketViewModelService(IRepository<CatalogItem> itemRepository,
        IUriComposer uriComposer, IBasketClient basketClient)
    {
        _uriComposer = uriComposer;
        _basketClient = basketClient;
        _itemRepository = itemRepository;
    }

    public async Task<BasketViewModel> GetOrCreateBasketForUser(string userName)
    {
        var basket = await _basketClient.GetOrCreateBasketByBuyerId(userName);
        var viewModel = await Map(basket);
        return viewModel;
    }

    private async Task<List<BasketItemViewModel>> GetBasketItems(IReadOnlyCollection<BasketItemDTO> basketItems)
    {
        var catalogItemsSpecification = new CatalogItemsSpecification(basketItems.Select(b => b.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

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
        return new BasketViewModel
        {
            BuyerId = basket.BuyerId, Id = basket.Id, Items = await GetBasketItems(basket.Items)
        };
    }

    public async Task<int> CountTotalBasketItems(string username)
    {
        var counter = await _basketClient.CountTotalBasketItems(username);

        return counter;
    }
}
