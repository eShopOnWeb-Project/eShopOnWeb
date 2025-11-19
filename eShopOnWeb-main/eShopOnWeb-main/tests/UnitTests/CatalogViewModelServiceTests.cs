using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.eShopWeb.ApplicationCore.DTOs;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Web.Services;
using Microsoft.eShopWeb.Web.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests;
public class CatalogViewModelServiceTests
{
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly ILogger<CatalogViewModelService> _logger = Substitute.For<ILogger<CatalogViewModelService>>();
    private readonly IUriComposer _uriComposer = Substitute.For<IUriComposer>();
    private readonly ICatalogApiClient _catalogApiClient = Substitute.For<ICatalogApiClient>();

    private CatalogViewModelService CreateService()
    {
        _loggerFactory.CreateLogger<CatalogViewModelService>().Returns(_logger);
        return new CatalogViewModelService(_loggerFactory, _uriComposer, _catalogApiClient);
    }

    [Fact]
    public async Task GetBrands_ReturnsAllPlusSortedBrands()
    {
        // Arrange
        var brand1 = new CatalogBrandDTO {Id = 2 ,Name =  "Beta" };
        var brand2 = new CatalogBrandDTO { Id = 1, Name = "Alpha" };

        var brands = new List<CatalogBrandDTO> { brand1, brand2 };
        _catalogApiClient.GetBrandsAsync().Returns(Task.FromResult(brands));

        var service = CreateService();

        // Act
        var result = (await service.GetBrands()).ToList();

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal("All", result[0].Text);
        Assert.True(result[0].Selected);
        Assert.Equal("Alpha", result[1].Text);
        Assert.Equal("Beta", result[2].Text);
        Assert.Equal("1", result[1].Value);
        Assert.Equal("2", result[2].Value);
    }

    [Fact]
    public async Task GetTypes_ReturnsAllPlusSortedTypes()
    {
        // Arrange
        var type1 = new CatalogTypeDTO { Id = 2, Type = "TypeB" };
        var type2 = new CatalogTypeDTO { Id = 1, Type = "TypeA" };

        var types = new List<CatalogTypeDTO> { type1, type2 };
        _catalogApiClient.GetCatalogTypesAsync().Returns(Task.FromResult(types));

        var service = CreateService();

        // Act
        var result = (await service.GetTypes()).ToList();

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal("All", result[0].Text);
        Assert.True(result[0].Selected);
        Assert.Equal("TypeA", result[1].Text);
        Assert.Equal("TypeB", result[2].Text);
        Assert.Equal("1", result[1].Value);
        Assert.Equal("2", result[2].Value);
    }

    [Fact]
    public async Task GetCatalogItems_ReturnsExpectedViewModel()
    {
        // Arrange
        var item1 = new CatalogItemDTO
        {
            Id = 1,
            CatalogTypeId = 1,
            CatalogBrandId = 1,
            Description = "desc1",
            Name = "Item1",
            Price = 10m,
            PictureUri = "pic1.jpg"
        };

        var item2 = new CatalogItemDTO
        {
            Id = 2,
            CatalogTypeId = 2,
            CatalogBrandId = 2,
            Description = "desc2",
            Name = "Item2",
            Price = 20m,
            PictureUri = "pic2.jpg"
        };

        var catalogItems = new List<CatalogItemDTO> { item1, item2 };
        var catalogResponse = new ListPagedCatalogItemResponse
        {
            CatalogItems = catalogItems,
            PageCount = 2
        };
        _catalogApiClient.GetCatalogItemsAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
            .Returns(Task.FromResult(catalogResponse));

        _uriComposer.ComposePicUri(Arg.Any<string>()).Returns(call => $"http://cdn/{call.Arg<string>()}");

        var service = CreateService();

        // Act
        var vm = await service.GetCatalogItems(0, 10);

        // Assert
        Assert.NotNull(vm);
        Assert.Equal(2, vm.CatalogItems.Count);
        Assert.Equal("http://cdn/pic1.jpg", vm.CatalogItems[0].PictureUri);
        Assert.Equal(10m, vm.CatalogItems[0].Price);
        Assert.Equal(2, vm.PaginationInfo.TotalPages);
        Assert.Equal("is-disabled", vm.PaginationInfo.Previous);
        Assert.Equal("", vm.PaginationInfo.Next);
    }
}

public class CatalogItemsResponse
{
    public IEnumerable<CatalogItemDTO> CatalogItems { get; set; }
    public int PageCount { get; set; }
}
