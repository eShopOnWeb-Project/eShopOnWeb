using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Http;
public class BearerTokenHandler : DelegatingHandler
{
    private readonly string _token;
    public BearerTokenHandler(string token) => _token = token;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Sending request to {request.RequestUri}");
        Console.WriteLine($"Authorization: Bearer {_token}");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

        var response = await base.SendAsync(request, cancellationToken);

        Console.WriteLine($"Response status code: {response.StatusCode}");

        return response;
    }
}
