using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.DTOs;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;
public interface ICatalogApiClient
{
    Task<ListPagedCatalogItemResponse> GetCatalogItemsAsync(int pageIndex, int pageSize, int? brandId = null, int? typeId = null);
    Task<ListPagedCatalogItemResponse> GetCatalogItemsAsync();
    Task<List<CatalogBrandDTO>> GetBrandsAsync();
    Task<List<CatalogTypeDTO>> GetCatalogTypesAsync();
    Task<CatalogItemDTO> GetCatalogItemAsync(int catalogItemId);
    Task<CatalogItemDTO> UpdateCatalogItemAsync(CatalogItemDTO item);
}
