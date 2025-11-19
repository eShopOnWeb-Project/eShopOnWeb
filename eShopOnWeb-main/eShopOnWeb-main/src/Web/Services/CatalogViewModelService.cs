using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Web.ViewModels;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Web.Services;

/// <summary>
/// This is a UI-specific service so belongs in UI project. It does not contain any business logic and works
/// with UI-specific types (view models and SelectListItem types).
/// </summary>
public class CatalogViewModelService : ICatalogViewModelService
{
    private readonly ILogger<CatalogViewModelService> _logger;
    private readonly IUriComposer _uriComposer;
    private readonly ICatalogApiClient _catalogApiClient;

    public CatalogViewModelService(
        ILoggerFactory loggerFactory,
        IUriComposer uriComposer,
        ICatalogApiClient catalogApiClient)
    {
        _logger = loggerFactory.CreateLogger<CatalogViewModelService>();
        _uriComposer = uriComposer;
        _catalogApiClient = catalogApiClient;

    }

    public async Task<CatalogIndexViewModel> GetCatalogItems(
        int pageIndex, int itemsPage, int? brandId = null, int? typeId = null)
    {
        var result = await _catalogApiClient.GetCatalogItemsAsync(pageIndex, itemsPage, brandId, typeId);

        var vm = new CatalogIndexViewModel
        {
            CatalogItems = result.CatalogItems.Select(i => new CatalogItemViewModel
            {
                Id = i.Id,
                Name = i.Name,
                PictureUri = _uriComposer.ComposePicUri(i.PictureUri),
                Price = i.Price
            }).ToList(),
            Brands = (await GetBrands()).ToList(),
            Types = (await GetTypes()).ToList(),
            BrandFilterApplied = brandId ?? 0,
            TypesFilterApplied = typeId ?? 0,
            PaginationInfo = new PaginationInfoViewModel
            {
                ActualPage = pageIndex,
                ItemsPerPage = result.CatalogItems.Count,
                TotalItems = result.PageCount * itemsPage,
                TotalPages = result.PageCount
            }
        };

        vm.PaginationInfo.Next = (vm.PaginationInfo.ActualPage == vm.PaginationInfo.TotalPages - 1) ? "is-disabled" : "";
        vm.PaginationInfo.Previous = (vm.PaginationInfo.ActualPage == 0) ? "is-disabled" : "";

        return vm;
    }

    public async Task<IEnumerable<SelectListItem>> GetBrands()
    {
        _logger.LogInformation("GetBrands called.");

        var brands = await _catalogApiClient.GetBrandsAsync();

        var items = brands
            .Select(brand => new SelectListItem { Value = brand.Id?.ToString(), Text = brand.Name })
            .OrderBy(b => b.Text)
            .ToList();

        items.Insert(0, new SelectListItem { Value = null, Text = "All", Selected = true });

        return items;
    }

    public async Task<IEnumerable<SelectListItem>> GetTypes()
    {
        _logger.LogInformation("GetTypes called.");

        var types = await _catalogApiClient.GetCatalogTypesAsync();

        var items = types
            .Select(type => new SelectListItem { Value = type.Id.ToString(), Text = type.Type })
            .OrderBy(t => t.Text)
            .ToList();

        var allItem = new SelectListItem { Value = null, Text = "All", Selected = true };
        items.Insert(0, allItem);

        return items;
    }
}
