using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using grefurBackend.Events;
using grefurBackend.Events.Domain;
using grefurBackend.Events.Lifecycle;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using grefurBackend.Context;

namespace grefurBackend.Engines;

public class CacheWarmupEngine : IEventHandler<SystemReadyEvent>, IEventHandler<CustomerLoadedEvent>
{
    private readonly EventBus _eventBus;
    private readonly CacheService _cacheService;
    private readonly CustomerService _customerService;
    private readonly IDbContextFactory<MySqlContext> _contextFactory;
    private readonly ILogger<CacheWarmupEngine> _logger;

    public CacheWarmupEngine(
        EventBus eventBus,
        CacheService cacheService,
        CustomerService customerService,
        IDbContextFactory<MySqlContext> contextFactory,
        ILogger<CacheWarmupEngine> logger)
    {
        _eventBus = eventBus;
        _cacheService = cacheService;
        _customerService = customerService;
        _contextFactory = contextFactory;
        _logger = logger;

        _eventBus.Subscribe<SystemReadyEvent>(this);
        _eventBus.Subscribe<CustomerLoadedEvent>(this);
    }

    public async Task Handle(SystemReadyEvent evt)
    {
        _logger.LogInformation("[CacheWarmupEngine]: System ready, starting cache warmup...");

        var customers = await _customerService.GetAllActiveSubscribersAsync().ConfigureAwait(false);
        await WarmupCache(customers, evt.CorrelationId).ConfigureAwait(false);
    }

    public async Task Handle(CustomerLoadedEvent evt)
    {
        var customer = await _customerService.GetCustomerByIdAsync(evt.CustomerId).ConfigureAwait(false);
        if (customer == null) return;

        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var deviceIds = await context.GrefurDevices
            .Where(d => d.CustomerId == customer.CustomerId && !d.IsDeletedByCustomer)
            .Select(d => d.DeviceId)
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var deviceId in deviceIds)
        {
            _cacheService.SetCustomerForDevice(deviceId, customer);
        }

        _logger.LogInformation("[CacheWarmupEngine]: Cached {Count} devices for customer {CustomerId}", deviceIds.Count, customer.CustomerId);
    }

    private async Task WarmupCache(List<grefurBackend.Models.GrefurCustomer> customers, string correlationId)
    {
        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var allDevices = await context.GrefurDevices
            .Where(d => !d.IsDeletedByCustomer)
            .Select(d => new { d.DeviceId, d.CustomerId })
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var customer in customers)
        {
            var customerDevices = allDevices.Where(d => d.CustomerId == customer.CustomerId);
            foreach (var device in customerDevices)
            {
                _cacheService.SetCustomerForDevice(device.DeviceId, customer);
            }
        }

        _logger.LogInformation("[CacheWarmupEngine]: Cache warmup completed. Total devices indexed: {Count}", allDevices.Count);
    }
}