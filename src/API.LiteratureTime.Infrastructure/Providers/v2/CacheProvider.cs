namespace API.LiteratureTime.Infrastructure.Providers.v2;

using System.Text.Json;
using API.LiteratureTime.Core.Interfaces.v2;
using StackExchange.Redis;

public class CacheProvider : ICacheProvider
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public CacheProvider(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var data = await db.StringGetAsync(key);
        if (data.IsNull)
            return default;

        return JsonSerializer.Deserialize<T>(data.ToString());
    }
}
