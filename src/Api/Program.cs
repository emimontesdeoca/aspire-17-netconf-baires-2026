using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient("cache");

builder.Services.AddHttpClient("sub-api", client =>
    client.BaseAddress = new Uri("https+http://sub-api"));

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/database-info", async (
    IHttpClientFactory clients,
    IConnectionMultiplexer redis,
    CancellationToken cancellationToken) =>
{
    const string key = "demo:database-info";
    var cache = redis.GetDatabase();
    var cached = await cache.StringGetAsync(key);

    if (cached.HasValue)
    {
        return cached.ToString();
    }

    var value = await clients.CreateClient("sub-api")
        .GetStringAsync("/database-info", cancellationToken);

    await cache.StringSetAsync(key, value, TimeSpan.FromMinutes(1));

    return value;
});

app.Run();
