using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.ApplicationCore.DTOs;

public class StockItemDTO
{
    [JsonPropertyName("itemId")]
    public int ItemId { get; set; }
    [JsonPropertyName("total")]
    public int Total { get; set; }
    [JsonPropertyName("reserved")]
    public int Reserved { get; set; }
}
