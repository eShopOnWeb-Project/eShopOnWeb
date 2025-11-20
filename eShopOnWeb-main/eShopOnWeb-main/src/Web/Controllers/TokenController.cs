using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.Infrastructure.Authentication;
using Microsoft.Extensions.Logging;

[ApiController]
[Route("api/token")]
public class TokenController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly ILogger<TokenController> _logger;

    public TokenController(TokenService tokenService, ILogger<TokenController> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpGet("gateway")]
    public IActionResult GetGatewayToken()
    {
        _logger.LogInformation("Gateway token requested.");
        var token = _tokenService.GenerateToken();
        _logger.LogInformation("Gateway token issued.");
        return Ok(token);
    }
}
