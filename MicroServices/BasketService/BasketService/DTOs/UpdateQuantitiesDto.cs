namespace BasketService.DTOs;

public class UpdateQuantitiesDto(int basketId, Dictionary<string, int> quantities)
{
    public int BasketId { get; set; } = basketId;
    public Dictionary<string, int> Quantities { get; set; } = quantities;
}
