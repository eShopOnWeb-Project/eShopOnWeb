using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.DTOs;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.eShopWeb.Web.ViewModels;
using Microsoft.eShopWeb.Web.Extensions;

namespace Microsoft.eShopWeb.Web.Services;

public class CatalogItemViewModelService : ICatalogItemViewModelService
{
    private readonly ICatalogApiClient _catalogApiClient;

    public CatalogItemViewModelService(ICatalogApiClient catalogApiClient)
    {
        _catalogApiClient = catalogApiClient;
    }

    public async Task UpdateCatalogItem(CatalogItemViewModel viewModel)
    {
        var dto = viewModel.ToDTO();
        await _catalogApiClient.UpdateCatalogItemAsync(dto);
    }
}
