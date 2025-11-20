using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Contracts.Orders;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Authentication;
using Microsoft.eShopWeb.Infrastructure.Caching;
using Microsoft.eShopWeb.Infrastructure.Http.Clients;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.Interfaces;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.Services;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure;

public static class Dependencies
{
    public static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        // Add Identity DbContext
        services.AddDbContext<AppIdentityDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("IdentityConnection")));

        services.AddTransient<IEmailSender, EmailSender>();

        // Configure Token Service
        var secretKeyPath = "/secrets/jwt/secret.key";
        if (!File.Exists(secretKeyPath))
        {
            throw new Exception($"Secret key not found at: {secretKeyPath}");
        }
        string secretKey = File.ReadAllText(secretKeyPath).Trim();
        services.AddSingleton(new TokenService(secretKey));

        // Register HTTP Clients
        services.AddScoped<ICatalogApiClient, CatalogApiClient>();
        services.AddScoped<IOrderServiceClient, OrderServiceClient>();
        services.AddScoped<IBasketClient, BasketClient>();

        // Register Caching
        services.AddSingleton<StockCache>();

        // Configure RabbitMQ
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));
        services.AddSingleton<IRabbitMqService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new RabbitMqService(options);
        });
    }
}
