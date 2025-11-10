using BasketService.DTOs;
using BasketService.Enitites;
using Microsoft.AspNetCore.Mvc;

namespace BasketService;

[ApiController]
[Route("/api/[controller]")]
public class BasketController(BasketRepository basketRepository) : ControllerBase
{
    [HttpGet("{basketId}")]
    public async Task<ActionResult<Basket>> GetBasket(int basketId)
    {
        var basket = await basketRepository.FindAsync(basketId);
        if (basket == null)
        {
            return NotFound();
        }
        return Ok(basket);
    }
    
    [HttpGet("getOrCreate/{buyerId}")]
    public async Task<ActionResult<Basket>> GetOrCreateBasket(string buyerId)
    {
        var basket = await basketRepository.GetOrCreateBasketByUsername(buyerId);
        return Ok(basket);
    }
    
    [HttpGet("count/{buyerId}")]
    public async Task<ActionResult<int>> GetBasketItemCount(string buyerId)
    {
        var count = await basketRepository.CountTotalBasketItems(buyerId);
        return Ok(count);
    }
    
    [HttpPost("addItem")]
    public async Task<ActionResult<Basket>> AddItemToBasket(AddBasketItemDto addBasketItemDto)
    {
        var basket = await basketRepository.GetOrCreateBasketByUsername(addBasketItemDto.Username);
        basket.AddItem(addBasketItemDto.CatalogItemId, addBasketItemDto.Price, addBasketItemDto.Quantity);
        
        await basketRepository.Update(basket);
        
        return RedirectToAction("GetOrCreateBasket", new { buyerId = addBasketItemDto.Username });
    }
    
    [HttpPatch("setQuantities")]
    public async Task<ActionResult<Basket>> SetQuantities(UpdateQuantitiesDto dto)
    {
        var basket = await basketRepository.FindAsync(dto.BasketId);
        if (basket == null) return NotFound();

        foreach (var item in basket.Items)
        {
            if (dto.Quantities.TryGetValue(item.Id.ToString(), out var quantity))
            {
                // todo: logger if (_logger != null) _logger.LogInformation($"Updating quantity of item ID:{item.Id} to {quantity}.");
                item.SetQuantity(quantity);
            }
        }
        basket.RemoveEmptyItems();
        await basketRepository.Update(basket);
        return Ok(basket);
    }
    
    [HttpPut(("transfer"))]
    public async Task<ActionResult> TransferBasket(TransferDTO dto)
    {
        await basketRepository.TransferBasketAsync(dto.AnonymousId,  dto.UserName);

        return Ok();
    }

    [HttpDelete("{basketId}")]
    public async Task<ActionResult> DeleteBasket(int basketId)
    {
        var basket = await basketRepository.FindAsync(basketId);

        if (basket != null)
        {
            await basketRepository.DeleteAsync(basket);

        }

        return NoContent();
    }
}
