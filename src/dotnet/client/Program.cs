// See https://aka.ms/new-console-template for more information

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var endpoints = new string[] {
    "http://localhost:5295/api/v1/endpoint1",
    "http://localhost:5295/api/v2/endpoint2"
};

var handler = new RoundRobinRetryHandler(endpoints);
var client = new HttpClient(handler);

var promptRequestJSON = JsonSerializer.Serialize(new PromptRequest("Hello, World!"));

while (true)
{
    var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5295/api/v1/endpoint1")
    {
        Content = new StringContent(promptRequestJSON, Encoding.UTF8, "application/json")
    };
    request.Headers.TryAddWithoutValidation("api-key", "0f306d4c-d6d8-4d2b-a1ec-24a2e5b3427d");
    var resp = await client.SendAsync(request);
    if (resp.IsSuccessStatusCode)
    {
        var response = JsonSerializer.Deserialize<CompletionResponse>(await resp.Content.ReadAsStringAsync());
        Console.WriteLine(response?.content);
    }
    await Task.Delay(200);
}


public class RoundRobinRetryHandler : DelegatingHandler
{
    private readonly string[] _endpoints;
    private readonly int retries;
    private readonly int delay;
    private int _currentEndpointIndex = 0;

    public RoundRobinRetryHandler(string[] endpoints, int retries = 3, int delay = 5) : base(new HttpClientHandler())
    {
        _endpoints = endpoints;
        this.retries = retries;
        this.delay = delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int idx = 0;
        for (int i = 0; i < this.retries; i++)
        {
            idx = _currentEndpointIndex % _endpoints.Length;
            request.RequestUri = new Uri(_endpoints[idx]);
            //request.RequestUri = new Uri("http://localhost:5295/api/endpoint1");

            Console.WriteLine($"{idx} Uri: {request.RequestUri}");

            var response = await base.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _currentEndpointIndex++;
                if (response.Headers.TryGetValues("Retry-After", out var timeout))
                {
                    if (int.TryParse(timeout.First(), out var seconds))
                    {
                        Console.WriteLine($"Waiting {seconds} seconds");
                        await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
                        continue;
                    }
                }
            }

            // Use the default delay
            await Task.Delay(TimeSpan.FromSeconds(this.delay), cancellationToken);

        }

        throw new HttpRequestException($"Request failed after {retries} retries");
    }
}

record PromptRequest(string prompt, int max_tokens = 100, double temperature = 0.3);
record CompletionResponse(string content);