using BasketService.Data;
using BasketService.Enitites;
using Microsoft.EntityFrameworkCore;

namespace BasketService;

public class BasketRepository(BasketContext context)
{
    public async Task<Basket> GetOrCreateBasketByUsername(string buyerId)
    {
        var basket = await context.Baskets
            .Where(b => b.BuyerId == buyerId)
            .Include(b => b.Items)
            .FirstOrDefaultAsync();

        if (basket == null)
        {
            basket =  CreateBasket(new Basket(buyerId)).Result; 
        }

        return basket;
    }

    private async Task<Basket> CreateBasket(Basket basket)
    {
        var addedBasket = context.Baskets.Add(basket);
        await context.SaveChangesAsync();

        return addedBasket.Entity;
    }

    public async Task<int> CountTotalBasketItems(string buyerId)
    {
        return await context.Baskets
            .Where(b => b.BuyerId == buyerId)
            .SelectMany(b => b.Items)
            .SumAsync(i => i.Quantity);
    }


    public async Task Update(Basket basket)
    {
        context.Baskets.Update(basket);

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Basket basket)
    {
        context.Baskets.Remove(basket);
        await context.SaveChangesAsync();
    }

    public async Task TransferBasketAsync(string anonymousId, string userName)
    {
        var anonymousBasket = await context.Baskets
            .Where(b => b.BuyerId == anonymousId)
            .Include(b => b.Items)
            .FirstOrDefaultAsync();

        if (anonymousBasket == null) return;

        var userBasket = await context.Baskets
                             .Where(b => b.BuyerId == userName)
                             .Include(b => b.Items)
                             .FirstOrDefaultAsync()
                         ?? new Basket(userName);

        foreach (var item in anonymousBasket.Items)
        {
            userBasket.AddItem(item.CatalogItemId, item.UnitPrice, item.Quantity);
        }

        _ = CreateBasket(userBasket);
        _ = DeleteAsync(anonymousBasket);
    }

    public async Task<Basket?> FindAsync(int basketId)
    {
        return await context.Baskets
            .Where(b => b.Id == basketId)
            .Include(b => b.Items)
            .FirstOrDefaultAsync();
    }

    public async Task<Basket?> FindBasketByBuyerIdAsync(string buyerId)
    {
        return await context.Baskets
            .Where(b => b.BuyerId == buyerId)
            .Include(b => b.Items)
            .FirstOrDefaultAsync();
    }
}
