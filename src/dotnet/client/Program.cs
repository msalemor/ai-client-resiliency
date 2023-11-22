using System.Text;
using System.Text.Json;
using common;
using Reliability;

var endpoints = new Tuple<string, string>[] {
    new("http://localhost:5295/api/v1/endpoint1",Guid.NewGuid().ToString()),
    new("http://localhost:5295/api/v2/endpoint2",Guid.NewGuid().ToString())
};

var handler = new RoundRobinRetryHandler(endpoints);
var client = new HttpClient(handler);

var promptRequestJSON = JsonSerializer.Serialize(new PromptRequest("What is the speed of light?"));

while (true)
{
    var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5295/api/v1/endpoint1")
    {
        Content = new StringContent(promptRequestJSON, Encoding.UTF8, "application/json")
    };
    var resp = await client.SendAsync(request);
    if (resp.IsSuccessStatusCode)
    {
        var response = JsonSerializer.Deserialize<CompletionResponse>(await resp.Content.ReadAsStringAsync());
        Console.WriteLine(response?.content);
    }
    await Task.Delay(200);
}
