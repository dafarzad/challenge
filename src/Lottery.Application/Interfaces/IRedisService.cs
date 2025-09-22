using System.Collections.Generic;

namespace Lottery.Application.Interfaces;

public interface IRedisService
{
    Task SetStringAsync(string key, string value, TimeSpan? expiry = null);
    Task<string?> GetStringAsync(string key);
    // Return hash entries as simple key/value pairs to avoid infra types leaking
    Task<KeyValuePair<string,string>[]> HashGetAllAsync(string key);
    Task HashSetAsync(string key, KeyValuePair<string,string>[] entries);
    Task<long> StringIncrementAsync(string key);
}
