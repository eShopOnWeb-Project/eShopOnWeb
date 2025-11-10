using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Contracts.Orders;
public class OrderItemDto
{
    [JsonPropertyName("itemordered_catalogitemid")]
    public int ItemOrdered_CatalogItemId { get; set; }

    [JsonPropertyName("itemordered_productname")]
    public string ItemOrdered_ProductName { get; set; }

    [JsonPropertyName("itemordered_pictureuri")]
    public string ItemOrdered_PictureUri { get; set; }

    [JsonPropertyName("unitprice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("units")]
    public int Units { get; set; }

    public OrderItemDto()
    {
    }
}
