using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Contracts.Orders;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IOrderServiceClient _orderServiceClient;
    private readonly ICatalogApiClient _catalogApiClient;
    private readonly IUriComposer _uriComposer;
    private readonly IBasketClient _basketClient;

    public OrderService(
        ICatalogApiClient catalogApiClient,
        IUriComposer uriComposer,
        IOrderServiceClient orderServiceClient, 
        IBasketClient basketClient)
    {
        _catalogApiClient = catalogApiClient;
        _uriComposer = uriComposer;
        _orderServiceClient = orderServiceClient;
        _basketClient = basketClient;
    }
    
    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basket = await _basketClient.GetBasket(basketId);
        if (basket == null || basket.Items.Count == 0) throw new InvalidOperationException("Basket is empty or not found.");

        var catalogItemsResponse = await _catalogApiClient.GetCatalogItemsAsync();
        var catalogItems = catalogItemsResponse.CatalogItems;
        var basketItemIds = basket.Items.Select(i => i.CatalogItemId).ToHashSet();
        catalogItems = catalogItems
            .Where(ci => basketItemIds.Contains(ci.Id))
            .ToList();

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            return new OrderItemDto
            {
                ItemOrdered_CatalogItemId = catalogItem.Id,
                ItemOrdered_ProductName = catalogItem.Name,
                ItemOrdered_PictureUri = _uriComposer.ComposePicUri(catalogItem.PictureUri),
                UnitPrice = basketItem.UnitPrice,
                Units = basketItem.Quantity
            };
        }).ToList();

        var createOrderDto = new CreateOrderDto
        {
            BuyerId = basket.BuyerId,
            Shipping = new ShippingAddressDto
            {
                Street = shippingAddress.Street,
                City = shippingAddress.City,
                State = shippingAddress.State,
                Country = shippingAddress.Country,
                Zip = shippingAddress.ZipCode
            },
            Items = items
        };

        await _orderServiceClient.CreateOrderAsync(createOrderDto);
    }
}
