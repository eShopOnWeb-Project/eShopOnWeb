namespace Microsoft.eShopWeb.ApplicationCore.DTOs.Basket;

public class AddBasketItemDto(string username, int catalogItemId, decimal price, int quantity = 1)
{
    public string Username { get; set; } = username;
    public int CatalogItemId { get; set; } = catalogItemId;
    public decimal Price { get; set; } = price;
    public int Quantity { get; set; } = quantity;
}
