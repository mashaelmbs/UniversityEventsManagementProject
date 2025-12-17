using Microsoft.Extensions.Caching.Memory;

namespace UniversityEventsManagement.Services
{
    public interface ICacheService
    {
        T? Get<T>(string key);
        Task<T?> GetAsync<T>(string key);
        void Set<T>(string key, T value, TimeSpan? expiration = null);
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
        void Remove(string key);
        Task RemoveAsync(string key);
        void Clear();
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly HashSet<string> _cacheKeys;

        public CacheService(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
            _cacheKeys = new HashSet<string>();
        }

        public T? Get<T>(string key)
        {
            if (_memoryCache.TryGetValue(key, out var value) && value is T typed)
            {
                return typed;
            }
            return default;
        }

        public Task<T?> GetAsync<T>(string key)
        {
            return Task.FromResult(Get<T>(key));
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            var cacheOptions = new MemoryCacheEntryOptions();
            
            if (expiration.HasValue)
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = expiration;
            }
            else
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            }

            _memoryCache.Set(key, value, cacheOptions);
            _cacheKeys.Add(key);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            Set(key, value, expiration);
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _memoryCache.Remove(key);
            _cacheKeys.Remove(key);
        }

        public Task RemoveAsync(string key)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Clear()
        {
            foreach (var key in _cacheKeys)
            {
                _memoryCache.Remove(key);
            }
            _cacheKeys.Clear();
        }
    }

    // Cache key constants
    public static class CacheKeys
    {
        public const string AllEvents = "all_events";
        public const string AllClubs = "all_clubs";
        public const string UserDashboard = "user_dashboard_{0}";
        public const string AdminDashboard = "admin_dashboard";
        public const string EventDetails = "event_details_{0}";
        public const string UserNotifications = "user_notifications_{0}";
        public const string SystemStatistics = "system_statistics";
    }
}
