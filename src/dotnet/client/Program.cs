using System.Text;
using System.Text.Json;
using common;
using Reliability;

var apiKey1 = Guid.NewGuid().ToString();
var apiKey2 = Guid.NewGuid().ToString();

var endpoints = new Tuple<string, string, int>[] {
    new("http://localhost:5295/api/v1/endpoint1",apiKey1,1),
    new("http://localhost:5295/api/v2/endpoint2",apiKey2,2)
};

var roundRobinHandler = new RoundRobinRetryHandler(endpoints);
var http = new HttpClient(roundRobinHandler);

var promptRequestJSON = JsonSerializer.Serialize(new PromptRequest("What is the speed of light?"));

while (true)
{
    var requestMessage = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5295/api/v1/endpoint1")
    {
        Content = new StringContent(promptRequestJSON, Encoding.UTF8, "application/json")
    };
    var response = await http.SendAsync(requestMessage);
    if (response.IsSuccessStatusCode)
    {
        var completion = JsonSerializer.Deserialize<CompletionResponse>(await response.Content.ReadAsStringAsync());
        Console.WriteLine(completion?.content);
    }
    await Task.Delay(200);
}
