using Lottery.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Linq;

namespace Lottery.Infra.redis;

public class RedisService : IRedisService
{
    private readonly ConnectionMultiplexer _mux;
    private readonly IDatabase _db;

    public RedisService(ConnectionMultiplexer mux, IConfiguration config)
    {
        _mux = mux;
        _db = _mux.GetDatabase();
    }

    public Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
        => _db.StringSetAsync(key, value, expiry);

    public async Task<string?> GetStringAsync(string key)
    {
        var v = await _db.StringGetAsync(key);
        if (v.IsNull) return null;
        return v.ToString();
    }

    public async Task<KeyValuePair<string,string>[]> HashGetAllAsync(string key)
    {
        var entries = await _db.HashGetAllAsync(key);
        return entries.Select(e => new KeyValuePair<string,string>(e.Name.ToString() ?? string.Empty, e.Value.ToString() ?? string.Empty)).ToArray();
    }

    public async Task HashSetAsync(string key, KeyValuePair<string,string>[] entries)
    {
        var hash = entries.Select(e => new HashEntry(e.Key, e.Value)).ToArray();
        await _db.HashSetAsync(key, hash);
    }

    public async Task<long> StringIncrementAsync(string key)
    {
        return (long)await _db.StringIncrementAsync(key);
    }
}
