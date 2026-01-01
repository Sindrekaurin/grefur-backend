using System.Threading.Tasks;
using grefurBackend.Events;
using grefurBackend.Events.Domain;
using grefurBackend.Events.Lifecycle;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;

namespace grefurBackend.Engines;

public class CustomerLoadEngine : IEventHandler<SystemReadyEvent>
{
    private readonly EventBus _eventBus;
    private readonly CustomerService _customerService;
    private readonly ILogger<CustomerLoadEngine> _logger;

    public CustomerLoadEngine(EventBus EventBus, CustomerService CustomerService, ILogger<CustomerLoadEngine> Logger)
    {
        _eventBus = EventBus;
        _customerService = CustomerService;
        _logger = Logger;

        // Register engine for lifecycle event
        _eventBus.Subscribe<SystemReadyEvent>(this);
    }

    public async Task Handle(SystemReadyEvent Evt)
    {
        _logger.LogInformation("[CustomerLoadEngine]: System ready, loading customers...");

        var Customers = await _customerService.GetAllActiveSubscribersAsync().ConfigureAwait(false);

        foreach (var Customer in Customers)
        {
            var CustomerLoadedEvent = new CustomerLoadedEvent(
                Customer: Customer,
                Source: nameof(CustomerLoadEngine),
                CorrelationId: Evt.CorrelationId
            );

            await _eventBus.Publish(CustomerLoadedEvent).ConfigureAwait(false);

            _logger.LogInformation("[CustomerLoadEngine]: Customer loaded: {CustomerId}", Customer.CustomerId);
        }
    }
}