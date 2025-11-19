using System.Net.Mime;
using Ardalis.ListStartupServices;
using BlazorAdmin;
using BlazorAdmin.Services;
using Blazored.LocalStorage;
using BlazorShared;
using BlazorShared.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Authentication;
using Microsoft.eShopWeb.Infrastructure.Caching;
using Microsoft.eShopWeb.Infrastructure.Http;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web;
using Microsoft.eShopWeb.Web.Configuration;
using Microsoft.eShopWeb.Web.Features.MyOrders;
using Microsoft.eShopWeb.Web.HealthChecks;
using Microsoft.eShopWeb.Web.Hubs;
using Microsoft.eShopWeb.Web.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);




// -----  Service Configuration  ------------------------

// Configure MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<GetMyOrdersHandler>();
});

// Configure Infrastructure Services
Microsoft.eShopWeb.Infrastructure.Dependencies.ConfigureServices(builder.Configuration, builder.Services);

// Configure HTTP Client for Gateway with Delegating Handler
builder.Services.AddHttpClient("Gateway", client =>
{
    client.BaseAddress = new Uri("http://gateway:8080");
})
.AddHttpMessageHandler(sp =>
{
    var tokenService = sp.GetRequiredService<TokenService>();
    var token = tokenService.GenerateToken();

    return new BearerTokenHandler(token);
});

// Configure Data Protection for Docker environment
if (builder.Environment.EnvironmentName == "Docker")
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/keys"))
        .SetApplicationName("eshopweb");
}

// Configure Authentication
builder.Services.AddCookieSettings();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;

        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// Configure Entity Framework and Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
           .AddDefaultUI()
           .AddEntityFrameworkStores<AppIdentityDbContext>()
                           .AddDefaultTokenProviders();

builder.Services.AddScoped<ITokenClaimsService, IdentityTokenClaimService>();
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddCoreServices(builder.Configuration);
builder.Services.AddWebServices(builder.Configuration);

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/octet-stream"]);
});

// Add memory cache services
builder.Services.AddMemoryCache();
builder.Services.AddRouting(options =>
{
    // Replace the type and the name used to refer to it with your own
    // IOutboundParameterTransformer implementation
    options.ConstraintMap["slugify"] = typeof(SlugifyParameterTransformer);
});

builder.Services.AddMvc(options =>
{
    options.Conventions.Add(new RouteTokenTransformerConvention(
             new SlugifyParameterTransformer()));

});
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizePage("/Basket/Checkout");
});
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddHealthChecks()
    .AddCheck<ApiHealthCheck>("api_health_check", tags: new[] { "apiHealthCheck" })
    .AddCheck<HomePageHealthCheck>("home_page_health_check", tags: new[] { "homePageHealthCheck" });
builder.Services.Configure<ServiceConfig>(config =>
{
    config.Services = new List<ServiceDescriptor>(builder.Services);
    config.Path = "/allservices";
});

// blazor configuration
var configSection = builder.Configuration.GetRequiredSection(BaseUrlConfiguration.CONFIG_NAME);
builder.Services.Configure<BaseUrlConfiguration>(configSection);
var baseUrlConfig = configSection.Get<BaseUrlConfiguration>();

// Blazor Admin Required Services for Prerendering
builder.Services.AddScoped<HttpClient>(s => new HttpClient
{
    BaseAddress = new Uri(baseUrlConfig!.WebBase)
});

// add blazor services
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<HttpService>();
builder.Services.AddBlazorServices(builder.Configuration);

builder.Services.AddDatabaseDeveloperPageExceptionFilter();





// -----  Application Configuration  ------------------------
var app = builder.Build();

app.Logger.LogInformation("App created...");

app.UseResponseCompression();

// Seed Identity Database
using (var scope = app.Services.CreateScope())
{
    var scopedProvider = scope.ServiceProvider;
    try
    {
        app.Logger.LogInformation("Seeding Identity Database...");
        var userManager = scopedProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scopedProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var identityContext = scopedProvider.GetRequiredService<AppIdentityDbContext>();
        await AppIdentityDbContextSeed.SeedAsync(identityContext, userManager, roleManager);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

// Initialize Stock Cache
using (var scope = app.Services.CreateScope())
{
    var scopedProvider = scope.ServiceProvider;
    try
    {
        app.Logger.LogInformation("Initializing Stock Cache...");
        var stockCache = scopedProvider.GetRequiredService<StockCache>();
        await stockCache.Initialize();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred initializing stock cache.");
    }
}

app.UseHealthChecks("/health",
    new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            var result = new
            {
                status = report.Status.ToString(),
                errors = report.Entries.Select(e => new
                {
                    key = e.Key,
                    value = Enum.GetName(typeof(HealthStatus), e.Value.Status)
                })
            }.ToJson();
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsync(result);
        }
    });

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Docker")
{
    app.Logger.LogInformation("Adding Development middleware...");
    app.UseDeveloperExceptionPage();
    app.UseShowAllServicesMiddleware();
    app.UseMigrationsEndPoint();
    app.UseWebAssemblyDebugging();
}
else
{
    app.Logger.LogInformation("Adding non-Development middleware...");
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();

app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller:slugify=Home}/{action:slugify=Index}/{id?}");
app.MapRazorPages();
app.MapHealthChecks("home_page_health_check", new HealthCheckOptions { Predicate = check => check.Tags.Contains("homePageHealthCheck") });
app.MapHealthChecks("api_health_check", new HealthCheckOptions { Predicate = check => check.Tags.Contains("apiHealthCheck") });
app.MapFallbackToFile("index.html");

app.MapHub<StockHub>("/stockhub");

app.Logger.LogInformation("LAUNCHING");
app.Run();




