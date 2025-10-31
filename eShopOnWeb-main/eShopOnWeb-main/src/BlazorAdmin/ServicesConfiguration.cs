using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using BlazorAdmin.Interfaces;
using BlazorAdmin.Services;
using BlazorShared;
using BlazorShared.Models;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorAdmin;

public static class ServicesConfiguration
{
    public static IServiceCollection AddBlazorServices(this IServiceCollection services, IConfiguration configuration)
    {
        var catalogSettings = configuration.Get<CatalogSettings>() ?? new CatalogSettings();
        services.AddSingleton<IUriComposer>(new UriComposer(catalogSettings));

        services.AddScoped<ICatalogBrandService, CachedCatalogBrandServiceDecorator>();
        services.AddScoped<CatalogBrandService>();

        services.AddScoped<ICatalogTypeService, CachedCatalogTypeServiceDecorator>();
        services.AddScoped<CatalogTypeService>();

        services.AddScoped<CatalogItemService>();
        services.AddScoped<ICatalogItemService, CachedCatalogItemServiceDecorator>();

        return services;
    }
}
