using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Core.Services
{
    public class DistributedCacheService
    {
        private readonly IDistributedCache _cache;

        public DistributedCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            var json = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(key, json, options);
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var json = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json);
        }

        public async Task RemoveAsync(string key)
        {
            await _cache.RemoveAsync(key);
        }

        public async Task<bool> ExistsAsync(string key)
        {
            var value = await _cache.GetStringAsync(key);
            return !string.IsNullOrEmpty(value);
        }
    }
}
