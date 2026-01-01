using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using grefurBackend.Models;
using Microsoft.Extensions.Logging;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using grefurBackend.Events.Queries;

namespace grefurBackend.Services
{
    public class CustomerService :
        IEventHandler<CustomerQueryEvent>,
        IEventHandler<RequestCustomerValueEnrichmentEvent>
    {
        private readonly ILogger<CustomerService> _logger;
        private readonly EventBus _eventBus;

        public CustomerService(ILogger<CustomerService> logger, EventBus eventBus)
        {
            _logger = logger;
            _eventBus = eventBus;

            _eventBus.Subscribe<CustomerQueryEvent>(this);
            _eventBus.Subscribe<RequestCustomerValueEnrichmentEvent>(this);
        }

        public async Task Handle(CustomerQueryEvent evt)
        {
            var customer = await GetCustomerByDeviceIdAsync(evt.DeviceId);
            if (customer != null)
            {
                // Vi bruker navngitte parametere som matcher konstruktøren i CustomerQuery.cs
                var response = new CustomerQueryResponseEvent(
                    deviceId: evt.DeviceId,
                    customerIdParam: customer.CustomerId, // Endret fra customerId til customerIdParam
                    source: nameof(CustomerService),
                    correlationId: evt.CorrelationId // Sørg for at CustomerQueryEvent har CorrelationId (PascalCase)
                );
                await _eventBus.Publish(response);
            }
        }

        public async Task Handle(RequestCustomerValueEnrichmentEvent evt)
        {
            _logger.LogInformation("[CustomerService]: Received RequestCustomerValueEnrichmentEvent query");

            _logger.LogInformation("[CustomerService]: Device: {deviceId}, Topic: {topic}, CorrelationId: {correlationId}",
                evt.DeviceId, evt.Topic, evt.CorrelationId);

            var customer = await GetCustomerByDeviceIdAsync(evt.DeviceId);

            if (customer == null)
            {
                // Note: evt.Customer.CustomerId used because evt.CustomerId string was removed
                _logger.LogWarning("[CustomerService]: Could not find customer for device {deviceId} during enrichment.", evt.DeviceId);
                return;
            }

            int alarmLevelValue = (int)customer.AlarmSubscription;
            int logLevelValue = (int)customer.LogSubscription;

            _logger.LogDebug("[CustomerService]: Mapping subscriptions for {customerId} - Alarm: {alarmEnum} ({alarmInt}), Log: {logEnum} ({logInt})",
                customer.CustomerId, customer.AlarmSubscription, alarmLevelValue, customer.LogSubscription, logLevelValue);

            // Changed CustomerId: customer to Customer: customer to match the new constructor
            var response = new ResponseCustomerValueEnrichmentEvent(
                Customer: customer,
                SubscriptionId: $"SUB-{customer.CustomerId}",
                AlarmPolicyLevel: customer.AlarmSubscription,
                LogPolicyLevel: customer.LogSubscription,
                Source: nameof(CustomerService),
                CorrelationId: evt.CorrelationId,
                DeviceId: evt.DeviceId,
                Topic: evt.Topic,
                Value: evt.Value,
                ValueType: evt.ValueType
            );

            _logger.LogDebug("[CustomerService]: Publishing response to EventBus with CorrelationId: {correlationId}", evt.CorrelationId);

            await _eventBus.Publish(response).ConfigureAwait(false);

            _logger.LogDebug("[CustomerService]: Successfully published levels for {customerId}. Log: {logLevel}, Alarm: {alarmLevel}",
                customer.CustomerId, logLevelValue, alarmLevelValue);
        }

        public async Task<List<GrefurCustomer>> GetAllActiveSubscribersAsync()
        {
            var AllCustomers = await GetAllCustomersMockAsync().ConfigureAwait(false);

            // Using the dynamic method to check for any active subscription or notification type
            var ActiveCustomers = AllCustomers
                .Where(C => C.IsActiveSubscriber())
                .ToList();

            _logger.LogInformation("Found {Count} active customers with dynamic subscription check.", ActiveCustomers.Count);

            foreach (var Customer in ActiveCustomers)
            {
                var DomainEvent = new CustomerLoadedEvent(
                    Customer: Customer,
                    Source: nameof(CustomerService),
                    CorrelationId: Guid.NewGuid().ToString()
                );

                await _eventBus.Publish(DomainEvent).ConfigureAwait(false);
            }

            return ActiveCustomers;
        }

        public async Task<GrefurCustomer?> GetCustomerByIdAsync(string customerId)
        {
            var customers = await GetAllCustomersMockAsync().ConfigureAwait(false);
            var customer = customers.FirstOrDefault(c => c.CustomerId == customerId);

            if (customer == null)
            {
                _logger.LogWarning("Customer {customerId} not found.", customerId);
            }

            return customer;
        }

        public async Task<GrefurCustomer?> GetCustomerByDeviceIdAsync(string deviceId)
        {
            var customers = await GetAllCustomersMockAsync().ConfigureAwait(false);
            var customer = customers.FirstOrDefault(c => c.RegisteredDevices.Contains(deviceId));

            if (customer == null)
            {
                _logger.LogWarning("Device {deviceId} is not registered to any Grefur customer.", deviceId);
            }

            return customer;
        }

        private Task<List<GrefurCustomer>> GetAllCustomersMockAsync()
        {
            return Task.FromResult(new List<GrefurCustomer>
            {
                new GrefurCustomer
                {
                    CustomerId = "CUST-001",
                    RegisteredDevices = new List<string>
                    {
                        "Grefur_3461",
                        "Grefur_235cfe"
                    },
                    LogSubscription = SubscriptionLevel.Normal,
                    AlarmSubscription = AlarmLevel.None,
                    NotificationTypes = NotificationTypes.None
                },
                new GrefurCustomer
                {
                    CustomerId = "CUST-002",
                    RegisteredDevices = new List<string> { "Grefur_3462" },
                    LogSubscription = SubscriptionLevel.None,
                    AlarmSubscription = AlarmLevel.None,
                    NotificationTypes = NotificationTypes.None
                }
            });
        }
    }
}