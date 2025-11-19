using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlazorAdmin.Extensions;
using BlazorShared;
using BlazorShared.Models;
using Microsoft.Extensions.Options;

namespace BlazorAdmin.Services;

public class HttpService
{
    private readonly HttpClient _httpClient;
    private readonly ToastService _toastService;
    private readonly string _catalogApi;
    private readonly GatewayTokenService _tokenService;


    public HttpService(HttpClient httpClient, IOptions<BaseUrlConfiguration> baseUrlConfiguration, ToastService toastService, GatewayTokenService tokenService)
    {
        _httpClient = httpClient;
        _toastService = toastService;
        _catalogApi = baseUrlConfiguration.Value.CatalogMicroservice;
        _tokenService = tokenService;

    }

    public async Task<T> HttpGet<T>(string uri) where T : class
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_catalogApi}/{uri}");
        var response = await SendRequestAsync(request);
        if (response == null) return null;

        return await FromHttpResponseMessage<T>(response);
    }

    public async Task<T> HttpDelete<T>(string uri, int id) where T : class
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{_catalogApi}/{uri}/{id}");
        var response = await SendRequestAsync(request);
        if (response == null) return null;

        return await FromHttpResponseMessage<T>(response);
    }

    public async Task<T> HttpPost<T>(string uri, object dataToSend) where T : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_catalogApi}/{uri}")
        {
            Content = ToJson(dataToSend)
        };
        var response = await SendRequestAsync(request);
        if (response == null) return null;

        return await FromHttpResponseMessage<T>(response);
    }

    public async Task<T> HttpPut<T>(string uri, object dataToSend) where T : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{_catalogApi}/{uri}")
        {
            Content = ToJson(dataToSend)
        };
        var response = await SendRequestAsync(request);
        if (response == null) return null;

        return await FromHttpResponseMessage<T>(response);
    }

    private StringContent ToJson(object obj)
    {
        return new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");
    }

    private async Task<T> FromHttpResponseMessage<T>(HttpResponseMessage result)
    {
        var data = JsonSerializer.Deserialize<T>(await result.Content.ReadAsStringAsync(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        return data;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request)
    {
        var token = await _tokenService.GetTokenAsync();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            try
            {
                var error = JsonSerializer.Deserialize<ErrorDetails>(errorContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (error != null)
                    _toastService.ShowToast($"Error: {error.Message}", ToastLevel.Error);
                else
                    _toastService.ShowToast($"Error: {response.StatusCode}", ToastLevel.Error);
            }
            catch
            {
                _toastService.ShowToast($"Error: {response.StatusCode}", ToastLevel.Error);
            }

            return null;
        }

        return response;
    }
}
