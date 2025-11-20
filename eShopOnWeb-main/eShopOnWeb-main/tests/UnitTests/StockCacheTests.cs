using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Caching;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests;
public class StockCacheTests
{
    private readonly IRabbitMqService _rabbitMqService = Substitute.For<IRabbitMqService>();
    private readonly StockCache _stockCache;

    public StockCacheTests()
    {
        _stockCache = new StockCache(_rabbitMqService, NullLogger<StockCache>.Instance);
    }

    [Fact]
    public async Task Initialize_ShouldPopulateCache_WithItemsFromService()
    {
        // Arrange
        var items = new List<RabbitMQFullDTOItem>
        {
            new RabbitMQFullDTOItem(1, 10, 2),
            new RabbitMQFullDTOItem(2, 5, 1)
        };
        _rabbitMqService.GetFullStockAsync().Returns(Task.FromResult(items));

        // Act
        await _stockCache.Initialize();

        // Assert
        var cachedItem1 = _stockCache.Get(1);
        Assert.NotNull(cachedItem1);
        Assert.Equal(10, cachedItem1.total);
        Assert.Equal(2, cachedItem1.reserved);

        var cachedItem2 = _stockCache.Get(2);
        Assert.NotNull(cachedItem2);
        Assert.Equal(5, cachedItem2.total);
        Assert.Equal(1, cachedItem2.reserved);
    }

    [Fact]
    public void Update_ShouldAddOrReplaceStockItem_ByItemId()
    {
        // Act
        _stockCache.Update(1, 20, 3);

        // Assert
        var stock = _stockCache.Get(1);
        Assert.NotNull(stock);
        Assert.Equal(20, stock.total);
        Assert.Equal(3, stock.reserved);

        // Replace existing
        _stockCache.Update(1, 15, 1);
        var updatedStock = _stockCache.Get(1);
        Assert.Equal(15, updatedStock.total);
        Assert.Equal(1, updatedStock.reserved);
    }

    [Fact]
    public void Update_ShouldAddOrReplaceStockItem_ByStockItem()
    {
        var stockItem = new RabbitMQFullDTOItem(2, 7, 2);

        _stockCache.Update(stockItem);

        var cached = _stockCache.Get(2);
        Assert.Equal(stockItem, cached);
    }

    [Fact]
    public void Get_ShouldReturnNull_WhenItemDoesNotExist()
    {
        var stock = _stockCache.Get(999);
        Assert.Null(stock);
    }

    [Fact]
    public void GetAll_ShouldReturnAllCachedItems()
    {
        _stockCache.Update(1, 10, 0);
        _stockCache.Update(2, 5, 1);

        var all = _stockCache.GetAll();

        Assert.Contains(all, item => item.itemId == 1);
        Assert.Contains(all, item => item.itemId == 2);
        Assert.Equal(2, all.Count);
    }
}
