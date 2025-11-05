using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.DTOs.Basket;

public class UpdateQuantitiesDto(int basketId, Dictionary<string, int> quantities)
{
    public int BasketId { get; set; } = basketId;
    public Dictionary<string, int> Quantities { get; set; } = quantities;
}
