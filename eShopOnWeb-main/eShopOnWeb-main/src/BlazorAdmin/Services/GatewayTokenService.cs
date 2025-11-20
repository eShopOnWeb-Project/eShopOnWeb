using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BlazorAdmin.Services;

public class GatewayTokenService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GatewayTokenService> _logger;
    private string _cachedToken;

    public GatewayTokenService(HttpClient httpClient, ILogger<GatewayTokenService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync()
    {
        if (_cachedToken != null)
        {
            _logger.LogDebug("Using cached gateway token.");
            return _cachedToken;
        }

        _logger.LogInformation("Requesting new gateway token.");
        _cachedToken = await _httpClient.GetStringAsync("api/token/gateway");
        _logger.LogInformation("Gateway token cached.");
        return _cachedToken;
    }
}

