using Microsoft.eShopWeb.ApplicationCore.DTOs;
using Microsoft.eShopWeb.Web.ViewModels;

namespace Microsoft.eShopWeb.Web.Extensions;

public static class CatalogItemExtensions
{
    public static CatalogItemDTO ToDTO(this CatalogItemViewModel catalogItemViewModel)
    {
        return new CatalogItemDTO
        {
            Id = catalogItemViewModel.Id,
            Name = catalogItemViewModel.Name,
            Price = catalogItemViewModel.Price,
            PictureUri = catalogItemViewModel.PictureUri
        };
    }
}
