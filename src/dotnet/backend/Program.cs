using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using common;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//SemaphoreSlim semaphore1 = new(1, 1);
object lockObject = new();
var rule = APIRule.None;

Dictionary<string, APISetting> values = [];

void setTimeout(string endpoint, DateTime ts)
{
    lock (lockObject)
    {
        if (!values.TryGetValue(endpoint, out APISetting? value))
        {
            values[endpoint] = new APISetting { Timeout = ts };
        }
        else
        {
            value.Timeout = ts;
        }
    }
}
void setAvailable(string endpoint, bool status)
{
    lock (lockObject)
    {
        if (!values.TryGetValue(endpoint, out APISetting? value))
        {
            values[endpoint] = new APISetting { Available = status };
        }
        else
        {
            value.Available = status;
        }
    }
}

void setAPIRule(string endpoint, APIRule rl)
{
    lock (lockObject)
    {
        if (!values.TryGetValue(endpoint, out APISetting? value))
        {
            values[endpoint] = new APISetting { Rule = rl };
        }
        else
        {
            value.Rule = rl;
        }
    }
}

APISetting getValues(string endpoint)
{
    values ??= [];
    if (!values.TryGetValue(endpoint, out APISetting? value))
    {
        value = new APISetting { Timeout = DateTime.UtcNow, Available = true, Rule = APIRule.None };
        values[endpoint] = value;
    }
    return value;
}

var clientRequestCounts = new Dictionary<string, Tuple<List<DateTime>, List<Tuple<int, DateTime>>>>();

// API Rule 0: Authentication - Key required
app.Use(async (context, next) =>
{
    bool failure = false;
    context.Request.Headers.TryGetValue("api-key", out var subID);
    if (string.IsNullOrEmpty(subID))
    {
        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        return;
    }

    // This is a dummy check to see if the subID is a valid GUID
    bool isValid = Guid.TryParse(subID, out Guid guid);
    if (!isValid)
    {
        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        return;
    }
    await next.Invoke();
});

// API Rule 1: Rate Limit
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api") && context.Request.Headers.TryGetValue("api-key", out var subID))
    {
        var diff = (int)(getValues(path).Timeout - DateTime.UtcNow).TotalSeconds;
        if (rule == APIRule.RateLimit && diff > 0)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.TryAdd("Retry-After", diff.ToString());
            return;
        }
        else if (rule == APIRule.RateLimit && diff <= 0)
        {
            setAvailable(path, true);
            setAPIRule(path, APIRule.None);
        }

        if (rule == APIRule.None)
        {
            if (!clientRequestCounts.ContainsKey(subID))
            {
                clientRequestCounts[subID] = new Tuple<List<DateTime>, List<Tuple<int, DateTime>>>([], []);
            }

            var requestCount = 0;
            lock (lockObject)
            {
                // Update the dictionary across threads
                var (ts, _) = clientRequestCounts[subID];
                ts.Add(DateTime.UtcNow);
                ts.RemoveAll(timestamp => timestamp < DateTime.UtcNow.AddMinutes(-1));
                requestCount = ts.Count;
            }

            if (requestCount > 50)
            {
                setTimeout(path, DateTime.UtcNow.AddMinutes(1));
                setAvailable(path, false);
                setAPIRule(path, APIRule.RateLimit);
                context.Response.StatusCode = 429;
                context.Response.Headers.TryAdd("Retry-After", "10");
                return;
            }
        }
    }

    await next.Invoke();
});

// API Rule 2: TPM
app.Use(async (context, next) =>
{
    var epPath = context.Request.Path;

    if (epPath.StartsWithSegments("/api") && context.Request.Headers.TryGetValue("api-key", out var subID))
    {

        var diff = (int)(getValues(epPath).Timeout - DateTime.UtcNow).TotalSeconds;
        if (rule == APIRule.TPM && diff > 0)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.TryAdd("Retry-After", diff.ToString());
            return;
        }
        else if (rule == APIRule.RateLimit && diff <= 0)
        {
            setAvailable(epPath, true);
            setAPIRule(epPath, APIRule.None);
        }

        if (rule == APIRule.None)
        {
            context.Request.EnableBuffering();
            var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            var request = JsonSerializer.Deserialize<PromptRequest>(body);
            context.Request.Body.Position = 0;

            if (request is not null)
            {
                if (!clientRequestCounts.ContainsKey(subID))
                {
                    clientRequestCounts[subID] = new Tuple<List<DateTime>, List<Tuple<int, DateTime>>>([], []);
                }

                int tokenCount = 0;
                lock (lockObject)
                {
                    var (_, counts) = clientRequestCounts[subID];
                    counts.Add(new Tuple<int, DateTime>(request.max_tokens, DateTime.UtcNow));
                    counts.RemoveAll(count => count.Item2 < DateTime.UtcNow.AddMinutes(-1));
                    tokenCount = counts.Sum(c => c.Item1);
                }

                if (tokenCount > 1000)
                {
                    context.Response.StatusCode = 429;
                    context.Response.Headers.TryAdd("Retry-After", "60");
                    setAPIRule(epPath, APIRule.TPM);
                    setAvailable(epPath, false);
                    setTimeout(epPath, DateTime.UtcNow.AddMinutes(1));
                    return;
                }
            }
        }
    }

    await next.Invoke();
});

var group = app.MapGroup("/api/v1");

group.MapPost("/endpoint1", ([FromHeader(Name = "api-key")][Required] string requiredHeader, [FromBody] PromptRequest request) =>
{
    return Results.Ok(new CompletionResponse("Hello World"));
})
.WithName("Endpoint1");

group.MapPost("/endpoint2", ([FromHeader(Name = "api-key")][Required] string requiredHeader, [FromBody] PromptRequest request) =>
{
    return Results.Ok(new CompletionResponse("Hello World"));
})
.WithName("Endpoint2");

app.Run();