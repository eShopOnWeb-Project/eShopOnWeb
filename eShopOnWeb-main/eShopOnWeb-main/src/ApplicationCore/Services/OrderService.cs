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
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IBasketClient _basketClient;

    public OrderService(
        IRepository<CatalogItem> itemRepository,
        IUriComposer uriComposer,
        IOrderServiceClient orderServiceClient, 
        IBasketClient basketClient)
    {
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
        _orderServiceClient = orderServiceClient;
        _basketClient = basketClient;
    }
    
    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basket = await _basketClient.GetBasket(basketId);
        if (basket == null || basket.Items.Count == 0) throw new InvalidOperationException("Basket is empty or not found.");

        var catalogItems = await _itemRepository.ListAsync(new CatalogItemsSpecification(basket.Items.Select(i => i.CatalogItemId).ToArray()));

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
