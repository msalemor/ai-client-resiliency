using System.Text;
using System.Text.Json;
using common;
using Reliability;

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
