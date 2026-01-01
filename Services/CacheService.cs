/*
 * CacheService.cs
 */
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using grefurBackend.Models;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;

namespace grefurBackend.Services;

public class CacheService : IEventHandler<CustomerLoadedEvent>, IDisposable
{
    private readonly ILogger<CacheService> _logger;
    private readonly EventBus _eventBus;
    private readonly CustomerService _customerService;

    private readonly ConcurrentDictionary<string, GrefurCustomer> _deviceToCustomerCache = new();
    private readonly ConcurrentDictionary<string, DateTime> _deviceCacheTimestamps = new();
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(10);
    private Timer? _cleanupTimer;

    public CacheService(ILogger<CacheService> logger, EventBus eventBus, CustomerService customerService)
    {
        _logger = logger;
        _eventBus = eventBus;
        _customerService = customerService;

        // Subscribe to CustomerLoadedEvent
        _eventBus.Subscribe<CustomerLoadedEvent>(this);

        StartCleanupTimer();
    }

    // Handle CustomerLoadedEvent to warm up cache
    public async Task Handle(CustomerLoadedEvent domainEvent)
    {
        // Merk: Antar CustomerLoadedEvent har Customer-objektet eller en CustomerId property med stor C
        var customerId = domainEvent.Customer?.CustomerId ?? "Unknown";
        _logger.LogInformation("[CacheService]: Warming cache for customer {CustomerId}", customerId);

        try
        {
            // Use CustomerService to fetch the full customer (including devices)
            var customer = await _customerService.GetCustomerByIdAsync(customerId).ConfigureAwait(false);
            if (customer == null)
            {
                _logger.LogWarning("[CacheService]: Customer {CustomerId} not found by CustomerService", customerId);
            }
            else
            {
                foreach (var deviceId in customer.RegisteredDevices ?? Enumerable.Empty<string>())
                {
                    _deviceToCustomerCache[deviceId] = customer;
                    _deviceCacheTimestamps[deviceId] = DateTime.UtcNow;
                    _logger.LogInformation("[CacheService]: Cached device {DeviceId} for customer {CustomerId}", deviceId, customer.CustomerId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CacheService]: Error while warming cache for customer {CustomerId}", customerId);
        }

        // Publish CacheReadyEvent for this customer
        var cacheReadyEvent = new CacheReadyEvent(
            customerId: customerId,
            source: nameof(CacheService),
            correlationId: domainEvent.CorrelationId
        );

        await _eventBus.Publish(cacheReadyEvent).ConfigureAwait(false);
    }

    public GrefurCustomer? GetCustomerForDevice(string deviceId)
    {
        if (_deviceToCustomerCache.TryGetValue(deviceId, out var customer) &&
            !IsCacheExpired(deviceId))
        {
            _logger.LogTrace("[CacheService]: Cache hit for device {DeviceId}", deviceId);
            return customer;
        }

        _logger.LogTrace("[CacheService]: Cache miss for device {DeviceId}", deviceId);
        return null;
    }

    public void SetCustomerForDevice(string deviceId, GrefurCustomer customer)
    {
        _deviceToCustomerCache[deviceId] = customer;
        _deviceCacheTimestamps[deviceId] = DateTime.UtcNow;
        _logger.LogDebug("[CacheService]: Cached customer for device {DeviceId}", deviceId);
    }

    public void RemoveDevice(string deviceId)
    {
        _deviceToCustomerCache.TryRemove(deviceId, out _);
        _deviceCacheTimestamps.TryRemove(deviceId, out _);
        _logger.LogDebug("[CacheService]: Removed device {DeviceId} from cache", deviceId);
    }

    public bool ContainsDevice(string deviceId)
    {
        return _deviceToCustomerCache.ContainsKey(deviceId) && !IsCacheExpired(deviceId);
    }

    public List<GrefurCustomer> GetAllCachedCustomers()
    {
        return _deviceToCustomerCache.Values
            .DistinctBy(c => c.CustomerId)
            .ToList();
    }

    public List<string> GetAllCachedDeviceIds()
    {
        return _deviceToCustomerCache.Keys.ToList();
    }

    public void Clear()
    {
        _deviceToCustomerCache.Clear();
        _deviceCacheTimestamps.Clear();
        _logger.LogInformation("[CacheService]: Cache cleared");
    }

    public CacheStatistics GetStatistics()
    {
        var timestamps = _deviceCacheTimestamps.Values;
        return new CacheStatistics
        {
            TotalEntries = _deviceToCustomerCache.Count,
            ValidEntries = _deviceToCustomerCache.Count(kvp => !IsCacheExpired(kvp.Key)),
            OldestEntry = timestamps.Any() ? timestamps.Min() : DateTime.MinValue,
            NewestEntry = timestamps.Any() ? timestamps.Max() : DateTime.MinValue
        };
    }

    private bool IsCacheExpired(string deviceId)
    {
        if (!_deviceCacheTimestamps.TryGetValue(deviceId, out var timestamp))
            return true;

        return DateTime.UtcNow - timestamp > _cacheTimeout;
    }

    private void StartCleanupTimer()
    {
        _cleanupTimer = new Timer(
            _ => CleanupExpiredEntries(),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5)
        );

        _logger.LogDebug("[CacheService]: Cache cleanup timer started");
    }

    private void CleanupExpiredEntries()
    {
        try
        {
            var expiredKeys = _deviceToCustomerCache.Keys
                .Where(IsCacheExpired)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _deviceToCustomerCache.TryRemove(key, out _);
                _deviceCacheTimestamps.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("[CacheService]: Cleaned up {Count} expired cache entries", expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CacheService]: Error during cache cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _logger.LogInformation("[CacheService]: Disposed");
    }
}

public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int ValidEntries { get; set; }
    public DateTime OldestEntry { get; set; }
    public DateTime NewestEntry { get; set; }
}