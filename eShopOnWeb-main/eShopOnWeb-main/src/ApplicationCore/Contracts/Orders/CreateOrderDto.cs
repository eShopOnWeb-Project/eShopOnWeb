using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Contracts.Orders;
public class CreateOrderDto
{
    [JsonPropertyName("buyer_id")]
    public string BuyerId { get; set; }
    [JsonPropertyName("basket_id")]
    public int BasketId { get; set; }

    [JsonPropertyName("shipping")]
    public ShippingAddressDto Shipping { get; set; }

    [JsonPropertyName("items")]
    public List<OrderItemDto> Items { get; set; }
}
