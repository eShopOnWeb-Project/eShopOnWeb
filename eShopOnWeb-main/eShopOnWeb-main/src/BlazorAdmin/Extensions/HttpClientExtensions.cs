using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using BlazorAdmin.Services;

namespace BlazorAdmin.Extensions;

public static class HttpClientExtensions
{
    public static async Task AddBearerTokenAsync(this HttpClient client, GatewayTokenService tokenService)
    {
        var token = await tokenService.GetTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
