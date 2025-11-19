using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Contracts.Orders;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Web.ViewModels;

namespace Microsoft.eShopWeb.Web.Features.OrderDetails;

public class GetOrderDetailsHandler : IRequestHandler<GetOrderDetails, OrderDetailViewModel?>
{
    private readonly IOrderServiceClient _orderServiceClient;

    public GetOrderDetailsHandler(IOrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<OrderDetailViewModel?> Handle(GetOrderDetails request, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.GetOrderByIdAsync(request.OrderId);

        if (order == null) return null;

        return new OrderDetailViewModel
        {
            OrderNumber = order.Id,
            OrderDate = order.OrderDate,
            Total = order.Total,
            ShippingAddress = new Address(
                order.Shipping.Street,
                order.Shipping.City,
                order.Shipping.State,
                order.Shipping.Country,
                order.Shipping.Zip
            ),
            OrderItems = order.Items.Select(oi => new OrderItemViewModel
            {
                ProductId = oi.ItemOrdered_CatalogItemId,
                ProductName = oi.ItemOrdered_ProductName,
                UnitPrice = oi.UnitPrice,
                Units = oi.Units,
                PictureUrl = oi.ItemOrdered_PictureUri
            }).ToList()
        };
    }
}
