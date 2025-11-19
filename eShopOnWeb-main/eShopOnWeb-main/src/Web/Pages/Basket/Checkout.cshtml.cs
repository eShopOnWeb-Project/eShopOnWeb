using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.Interfaces;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;

namespace Microsoft.eShopWeb.Web.Pages.Basket;

[Authorize]
public class CheckoutModel : PageModel
{
    private readonly IBasketClient _basketClient;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOrderService _orderService;
    private string? _username = null;
    private readonly IBasketViewModelService _basketViewModelService;
    private readonly IAppLogger<CheckoutModel> _logger;
    private readonly IRabbitMqService _rabbitMqService;

    public CheckoutModel(IBasketViewModelService basketViewModelService,
        SignInManager<ApplicationUser> signInManager,
        IOrderService orderService,
        IBasketClient basketClient,
        IAppLogger<CheckoutModel> logger,
        IRabbitMqService rabbitMqService)
    {
        _signInManager = signInManager;
        _orderService = orderService;
        _basketViewModelService = basketViewModelService;
        _logger = logger;
        _basketClient = basketClient;
        _rabbitMqService = rabbitMqService;
    }

    public BasketViewModel BasketModel { get; set; } = new BasketViewModel();

    public async Task<IActionResult> OnGetAsync()
    {
        await SetBasketModelAsync();

        try
        {
            var rpcItems = BasketModel.Items.Select(i => new RabbitMQDefaultDTOItem
            {
                itemId = i.CatalogItemId,
                amount = i.Quantity
            }).ToList();

            var response = await _rabbitMqService.ReserveAsync(rpcItems);
            if (!response.success)
            {
                TempData["Error"] = $"Reservation failed: {response.reason}          [HELP: go to admin page to restock more items ]";
                return RedirectToPage("/Basket/Index");
            }
            return Page();
        }
        catch (TimeoutException)
        {
            TempData["Error"] = "Reservation timed out.";
            return RedirectToPage("/Basket/Index");
        }
    }

    public async Task<IActionResult> OnPostPayNowAsync(IEnumerable<BasketItemViewModel> items)
    {
        try
        {
            _logger.LogInformation("Pay now clicked by user.");

            await SetBasketModelAsync();

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var updateModel = items.ToDictionary(b => b.Id.ToString(), b => b.Quantity);
            
            await _basketClient.SetQuantities(BasketModel.Id, updateModel);
            await _orderService.CreateOrderAsync(BasketModel.Id, new Address("123 Main St.", "Kent", "OH", "United States", "44240"));
            await _basketClient.DeleteBasketAsync(BasketModel.Id);
        }
        catch (EmptyBasketOnCheckoutException emptyBasketOnCheckoutException)
        {
            _logger.LogWarning(emptyBasketOnCheckoutException.Message);
            return RedirectToPage("/Basket/Index");
        }

        return RedirectToPage("Success");
    }

    public async Task<IActionResult> OnPostCancelAsync(IEnumerable<BasketItemViewModel> items)
    {
        try
        {
            _logger.LogInformation("Payment cancelled by user.");

            await SetBasketModelAsync();

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var rpcItems = BasketModel.Items.Select(i => new RabbitMQDefaultDTOItem
            {
                itemId = i.CatalogItemId,
                amount = i.Quantity
            }).ToList();

            await _rabbitMqService.SendCancelAsync(rpcItems);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e.Message);
        }

        return RedirectToPage("/Basket/Index");
    }

    private async Task SetBasketModelAsync()
    {
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        if (_signInManager.IsSignedIn(HttpContext.User))
        {
            BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(User.Identity.Name);
        }
        else
        {
            GetOrSetBasketCookieAndUserName();
            BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(_username!);
        }
    }

    private void GetOrSetBasketCookieAndUserName()
    {
        if (Request.Cookies.ContainsKey(Constants.BASKET_COOKIENAME))
        {
            _username = Request.Cookies[Constants.BASKET_COOKIENAME];
        }
        if (_username != null) return;

        _username = Guid.NewGuid().ToString();
        var cookieOptions = new CookieOptions();
        cookieOptions.Expires = DateTime.Today.AddYears(10);
        Response.Cookies.Append(Constants.BASKET_COOKIENAME, _username, cookieOptions);
    }
}
