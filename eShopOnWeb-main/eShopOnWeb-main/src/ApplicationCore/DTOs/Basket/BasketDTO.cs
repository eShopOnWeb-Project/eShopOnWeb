using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.DTOs.Basket;

public class BasketDTO
{
    public int Id { get; set; }
    public string BuyerId { get; set; }
    public List<BasketItemDTO> Items { get; set; }
}
