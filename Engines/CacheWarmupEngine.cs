// CacheWarmupEngine.cs

using System.Collections.Generic;
using System.Threading.Tasks;
using grefurBackend.Events;
using grefurBackend.Events.Domain;
using grefurBackend.Events.Lifecycle;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;

namespace grefurBackend.Engines;

public class CacheWarmupEngine : IEventHandler<SystemReadyEvent>, IEventHandler<CustomerLoadedEvent>
{
    private readonly EventBus _eventBus;
    private readonly CacheService _cacheService;
    private readonly CustomerService _customerService;
    private readonly ILogger<CacheWarmupEngine> _logger;

    public CacheWarmupEngine(EventBus eventBus, CacheService cacheService, CustomerService customerService, ILogger<CacheWarmupEngine> logger)
    {
        _eventBus = eventBus;
        _cacheService = cacheService;
        _customerService = customerService;
        _logger = logger;

        // Registrer engine for relevante events
        _eventBus.Subscribe<SystemReadyEvent>(this);
        _eventBus.Subscribe<CustomerLoadedEvent>(this);
    }

    // Fyll cache når systemet er klart
    public async Task Handle(SystemReadyEvent evt)
    {
        _logger.LogInformation("[CacheWarmupEngine]: System ready, starting cache warmup...");

        var customers = await _customerService.GetAllActiveSubscribersAsync().ConfigureAwait(false);
        _logger.LogInformation("[CacheWarmupEngine]: Customer count: {Count}", customers.Count);

        // Bruker PascalCase: CorrelationId fra base Event
        await WarmupCache(customers, evt.CorrelationId).ConfigureAwait(false);
    }

    // Fyll cache for hver kunde som lastes
    public async Task Handle(CustomerLoadedEvent evt)
    {
        // Bruker PascalCase: CustomerId fra CustomerLoadedEvent
        _logger.LogInformation("[CacheWarmupEngine]: CustomerLoadedEvent received: {CustomerId}", evt.CustomerId);

        var customer = await _customerService.GetCustomerByIdAsync(evt.CustomerId).ConfigureAwait(false);
        if (customer != null && customer.RegisteredDevices != null)
        {
            foreach (var deviceId in customer.RegisteredDevices)
            {
                _cacheService.SetCustomerForDevice(deviceId, customer);
                _logger.LogInformation("[CacheWarmupEngine]: Cached device {DeviceId} for customer {CustomerId}", deviceId, customer.CustomerId);
            }
        }
    }

    private async Task WarmupCache(List<grefurBackend.Models.GrefurCustomer> customers, string correlationId)
    {
        foreach (var customer in customers)
        {
            if (customer.RegisteredDevices == null) continue;

            foreach (var deviceId in customer.RegisteredDevices)
            {
                _cacheService.SetCustomerForDevice(deviceId, customer);
                _logger.LogInformation("[CacheWarmupEngine]: Cached device {DeviceId} for customer {CustomerId}", deviceId, customer.CustomerId);
            }
        }

        _logger.LogInformation("[CacheWarmupEngine]: Cache warmup completed for {Count} customers", customers.Count);
        await Task.CompletedTask;
    }
}