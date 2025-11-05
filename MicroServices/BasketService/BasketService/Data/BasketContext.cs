using System.Reflection;
using BasketService.Enitites;
using Microsoft.EntityFrameworkCore;

namespace BasketService.Data;

public class BasketContext(DbContextOptions<BasketContext> options) : DbContext(options)
{
    public DbSet<Basket> Baskets { get; set; }
    public DbSet<BasketItem> BasketItems { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
