using Microsoft.Extensions.Caching.Memory;
using FlightDelayApi.Models;
using System.Text.Json;

namespace FlightDelayApi.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task RemoveAsync(string key);
    string GeneratePredictionCacheKey(FlightDelayRequest request);
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheService> _logger;
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(30);

    public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out var cachedValue) && cachedValue is T value)
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return Task.FromResult<T?>(value);
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving from cache for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                SlidingExpiration = TimeSpan.FromMinutes(10),
                Priority = CacheItemPriority.Normal
            };

            _memoryCache.Set(key, value, options);
            _logger.LogDebug("Cached item with key: {Key}, expiration: {Expiration}", key, expiration ?? _defaultExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting cache for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        try
        {
            _memoryCache.Remove(key);
            _logger.LogDebug("Removed cache entry for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing cache entry for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public string GeneratePredictionCacheKey(FlightDelayRequest request)
    {
        return $"prediction_{request.DayOfWeek}_{request.OriginAirportID}_{request.DestAirportID}_{request.Month}_{request.Carrier}_{request.CRSDepTime}";
    }
}