namespace Reliability;

using System.Net;

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
            await Task.Delay(TimeSpan.FromSeconds(this.delay), cancellationToken);

        }

        throw new HttpRequestException($"Request failed after {retries} retries");
    }
}