using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Contracts.Orders;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Web.ViewModels;

namespace Microsoft.eShopWeb.Web.Features.MyOrders;

public class GetMyOrdersHandler : IRequestHandler<GetMyOrders, IEnumerable<OrderViewModel>>
{
    private readonly IOrderServiceClient _orderServiceClient;

    public GetMyOrdersHandler(IOrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<IEnumerable<OrderViewModel>> Handle(GetMyOrders request,
        CancellationToken cancellationToken)
    {
        var orders = await _orderServiceClient.GetOrdersForUserAsync(request.UserName);

        return orders.Select(o => new OrderViewModel
        {
            OrderDate = o.OrderDate,
            OrderNumber = o.Id,
            ShippingAddress = o.Shipping != null
                ? new Address(
                    o.Shipping.Street ?? "",
                    o.Shipping.City ?? "",
                    o.Shipping.State ?? "",
                    o.Shipping.Country ?? "",
                    o.Shipping.Zip ?? "")
                : null,
            Total = o.Total
        });
    }
}
