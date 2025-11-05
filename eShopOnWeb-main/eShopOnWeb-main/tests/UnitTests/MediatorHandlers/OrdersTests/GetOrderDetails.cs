//using System.Collections.Generic;
//using System.Threading;
//using System.Threading.Tasks;
//using Ardalis.Specification;
//using Microsoft.eShopWeb.ApplicationCore.Contracts.Orders;
//using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
//using Microsoft.eShopWeb.ApplicationCore.Interfaces;
//using Microsoft.eShopWeb.ApplicationCore.Specifications;
//using Microsoft.eShopWeb.Web.Features.OrderDetails;
//using NSubstitute;
//using Xunit;

//namespace Microsoft.eShopWeb.UnitTests.MediatorHandlers.OrdersTests;

//public class GetOrderDetails
//{
//    private readonly IOrderServiceClient _mockOrderRepository = Substitute.For<IOrderServiceClient>();

//    public GetOrderDetails()
//    {
//        var item = new OrderItem(new CatalogItemOrdered(1, "ProductName", "URI"), 10.00m, 10);
//        var address = new Address("", "", "", "", "");
//        OrderReadDto order = new OrderReadDto { Id = item.Id,  Shipping = new ShippingAddressDto { City = address.City }address, Items = new List<OrderItemDto> { item } };
                
//        _mockOrderRepository.GetOrderByIdAsync(Arg.Any<int>())
//            .Returns(order);
//    }

//    [Fact]
//    public async Task NotBeNullIfOrderExists()
//    {
//        var request = new eShopWeb.Web.Features.OrderDetails.GetOrderDetails("SomeUserName", 0);

//        var handler = new GetOrderDetailsHandler(_mockOrderRepository);

//        var result = await handler.Handle(request, CancellationToken.None);

//        Assert.NotNull(result);
//    }
//}
