using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.ApplicationCore.DTOs;

public class CatalogBrandDTO
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
