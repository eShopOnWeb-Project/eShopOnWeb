using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.Infrastructure.Services;

[ApiController]
[Route("api/token")]
public class TokenController : ControllerBase
{
    private readonly TokenService _tokenService;

    public TokenController(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpGet("gateway")]
    public IActionResult GetGatewayToken()
    {
        var token = _tokenService.GenerateToken();
        return Ok(token);
    }
}
