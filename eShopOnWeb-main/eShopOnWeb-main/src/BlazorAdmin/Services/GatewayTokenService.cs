using System.Net.Http;
using System.Threading.Tasks;

namespace BlazorAdmin.Services;

public class GatewayTokenService
{
    private readonly HttpClient _httpClient;
    private string _cachedToken;

    public GatewayTokenService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetTokenAsync()
    {
        if (_cachedToken != null)
            return _cachedToken;

        // Hent token fra server endpoint (server laver signering)
        _cachedToken = await _httpClient.GetStringAsync("api/token/gateway");
        return _cachedToken;
    }
}

