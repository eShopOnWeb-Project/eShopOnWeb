using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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

    public async Task<T> HttpGet<T>(string uri)
        where T : class
    {
        var token = await _tokenService.GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var result = await _httpClient.GetAsync($"{_catalogApi}/{uri}");
        if (!result.IsSuccessStatusCode)
        {
            return null;
        }

        return await FromHttpResponseMessage<T>(result);
    }

    public async Task<T> HttpDelete<T>(string uri, int id)
        where T : class
    {
        var token = await _tokenService.GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var result = await _httpClient.DeleteAsync($"{_catalogApi}/{uri}/{id}");
        if (!result.IsSuccessStatusCode)
        {
            return null;
        }

        return await FromHttpResponseMessage<T>(result);
    }

    public async Task<T> HttpPost<T>(string uri, object dataToSend)
        where T : class
    {
        var token = await _tokenService.GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var content = ToJson(dataToSend);

        var result = await _httpClient.PostAsync($"{_catalogApi}/{uri}", content);
        if (!result.IsSuccessStatusCode)
        {
            var exception = JsonSerializer.Deserialize<ErrorDetails>(await result.Content.ReadAsStringAsync(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            _toastService.ShowToast($"Error : {exception.Message}", ToastLevel.Error);

            return null;
        }

        return await FromHttpResponseMessage<T>(result);
    }

    public async Task<T> HttpPut<T>(string uri, object dataToSend)
        where T : class
    {
        var token = await _tokenService.GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var content = ToJson(dataToSend);

        var result = await _httpClient.PutAsync($"{_catalogApi}/{uri}", content);
        if (!result.IsSuccessStatusCode)
        {
            _toastService.ShowToast("Error", ToastLevel.Error);
            return null;
        }

        return await FromHttpResponseMessage<T>(result);
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
}
