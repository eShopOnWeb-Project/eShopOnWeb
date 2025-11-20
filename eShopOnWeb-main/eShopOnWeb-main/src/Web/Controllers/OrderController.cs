using Ardalis.GuardClauses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.Web.Features.MyOrders;
using Microsoft.eShopWeb.Web.Features.OrderDetails;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Web.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Authorize] // Controllers that mainly require Authorization still use Controller/View; other pages use Pages
[Route("[controller]/[action]")]
public class OrderController : Controller
{
    private readonly IMediator _mediator;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IMediator mediator, ILogger<OrderController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> MyOrders()
    {   
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        _logger.LogInformation("Fetching orders for user {UserName}.", User.Identity!.Name);
        var viewModel = await _mediator.Send(new GetMyOrders(User.Identity.Name));

        return View(viewModel);
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> Detail(int orderId)
    {
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        _logger.LogInformation("Fetching order {OrderId} details for user {UserName}.", orderId, User.Identity!.Name);
        var viewModel = await _mediator.Send(new GetOrderDetails(User.Identity.Name, orderId));

        if (viewModel == null)
        {
            _logger.LogWarning("Order {OrderId} not found for user {UserName}.", orderId, User.Identity.Name);
            return BadRequest("No such order found for this user.");
        }

        return View(viewModel);
    }
}
