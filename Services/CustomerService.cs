using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using grefurBackend.Models;
using grefurBackend.Context;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using grefurBackend.Events.Queries;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace grefurBackend.Services
{
    public class CustomerService :
        IEventHandler<CustomerQueryEvent>,
        IEventHandler<RequestCustomerValueEnrichmentEvent>
    {
        private readonly ILogger<CustomerService> _logger;
        private readonly EventBus _eventBus;
        private readonly MySqlContext _context;

        public CustomerService(ILogger<CustomerService> Logger, EventBus EventBus, MySqlContext Context)
        {
            _logger = Logger;
            _eventBus = EventBus;
            _context = Context;

            _eventBus.Subscribe<CustomerQueryEvent>(this);
            _eventBus.Subscribe<RequestCustomerValueEnrichmentEvent>(this);
        }

        public async Task<bool> CreateCustomer(GrefurCustomer Config)
        {
            try
            {
                await _context.GrefurCustomers.AddAsync(Config).ConfigureAwait(false);
                await _context.SaveChangesAsync().ConfigureAwait(false);
                return true;
            }
            catch (Exception Ex)
            {
                _logger.LogError(Ex, "Error creating customer {CustomerId}", Config.CustomerId);
                return false;
            }
        }

        public async Task<bool> EditCustomer(GrefurCustomer Config)
        {
            try
            {
                var Existing = await _context.GrefurCustomers
                    .FirstOrDefaultAsync(C => C.CustomerId == Config.CustomerId)
                    .ConfigureAwait(false);

                if (Existing == null) return false;

                Existing.OrganizationName = Config.OrganizationName;
                Existing.OrganizationNumber = Config.OrganizationNumber;
                Existing.IsEnabled = Config.IsEnabled;
                Existing.RegisteredDevices = Config.RegisteredDevices;
                Existing.LogSubscription = Config.LogSubscription;
                Existing.AlarmSubscription = Config.AlarmSubscription;
                Existing.NotificationTypes = Config.NotificationTypes;

                await _context.SaveChangesAsync().ConfigureAwait(false);
                return true;
            }
            catch (Exception Ex)
            {
                _logger.LogError(Ex, "Error editing customer {CustomerId}", Config.CustomerId);
                return false;
            }
        }

        public async Task<bool> DeleteCustomer(string CustomerId)
        {
            try
            {
                var Customer = await _context.GrefurCustomers
                    .FirstOrDefaultAsync(C => C.CustomerId == CustomerId)
                    .ConfigureAwait(false);

                if (Customer == null) return false;

                _context.GrefurCustomers.Remove(Customer);
                await _context.SaveChangesAsync().ConfigureAwait(false);
                return true;
            }
            catch (Exception Ex)
            {
                _logger.LogError(Ex, "Error deleting customer {CustomerId}", CustomerId);
                return false;
            }
        }

        public async Task Handle(CustomerQueryEvent Evt)
        {
            var Customer = await GetCustomerByDeviceIdAsync(Evt.DeviceId).ConfigureAwait(false);
            if (Customer != null)
            {
                var Response = new CustomerQueryResponseEvent(
                    deviceId: Evt.DeviceId,
                    customerIdParam: Customer.CustomerId,
                    source: nameof(CustomerService),
                    correlationId: Evt.CorrelationId
                );
                await _eventBus.Publish(Response).ConfigureAwait(false);
            }
        }

        public async Task Handle(RequestCustomerValueEnrichmentEvent Evt)
        {
            _logger.LogDebug("[CustomerService]: Received RequestCustomerValueEnrichmentEvent - Event details: {@event}", Evt);

            var Customer = await GetCustomerByDeviceIdAsync(Evt.DeviceId).ConfigureAwait(false);

            if (Customer == null)
            {
                _logger.LogWarning("[CustomerService]: Could not find customer for device {DeviceId} during enrichment.", Evt.DeviceId);
                return;
            }

            int alarmLevelValue = (int)Customer.AlarmSubscription;
            int logLevelValue = (int)Customer.LogSubscription;

            var Response = new ResponseCustomerValueEnrichmentEvent(
                Customer: Customer,
                SubscriptionId: $"SUB-{Customer.CustomerId}",
                AlarmPolicyLevel: Customer.AlarmSubscription,
                LogPolicyLevel: Customer.LogSubscription,
                Source: nameof(CustomerService),
                CorrelationId: Evt.CorrelationId,
                DeviceId: Evt.DeviceId,
                Topic: Evt.Topic,
                Value: Evt.Value,
                ValueType: Evt.ValueType
            );

            _logger.LogDebug("[CustomerService]: Publishing response to EventBus with CorrelationId: {CorrelationId}", Evt.CorrelationId);

            try
            {
                await _eventBus.Publish(Response).ConfigureAwait(false);
                _logger.LogDebug("[CustomerService]: Successfully published levels for {CustomerId}. Log: {LogLevel}, Alarm: {AlarmLevel}",
                    Customer.CustomerId, logLevelValue, alarmLevelValue);
            }
            catch (Exception Ex)
            {
                _logger.LogError(Ex, "[CustomerService]: Error publishing enrichment response for customer {CustomerId}", Customer.CustomerId);
            }
        }

        public async Task<List<GrefurCustomer>> GetAllActiveSubscribersAsync()
        {
            var AllCustomers = await GetAllCustomersMockAsync().ConfigureAwait(false);

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

        public async Task<GrefurCustomer?> GetCustomerByIdAsync(string CustomerId)
        {
            var Customers = await GetAllCustomersMockAsync().ConfigureAwait(false);
            var Customer = Customers.FirstOrDefault(C => C.CustomerId == CustomerId);

            if (Customer == null)
            {
                _logger.LogWarning("Customer {CustomerId} not found.", CustomerId);
            }

            return Customer;
        }

        public async Task<GrefurCustomer?> GetCustomerByDeviceIdAsync(string DeviceId)
        {
            var Customers = await GetAllCustomersMockAsync().ConfigureAwait(false);
            var Customer = Customers.FirstOrDefault(C => C.RegisteredDevices.Contains(DeviceId));

            if (Customer == null)
            {
                _logger.LogWarning("Device {DeviceId} is not registered to any Grefur customer.", DeviceId);
            }

            return Customer;
        }

        private async Task<List<GrefurCustomer>> GetAllCustomersMockAsync()
        {
            return await _context.GrefurCustomers.ToListAsync().ConfigureAwait(false);
        }

        private List<GrefurCustomer> GenerateTestCustomers(int Count)
        {
            var CustomerList = new List<GrefurCustomer>();

            for (int i = 1; i <= Count; i++)
            {
                string CustomerId = $"CUST-{(i + 2):D3}";

                var NewCustomer = new GrefurCustomer
                {
                    CustomerId = CustomerId,
                    RegisteredDevices = new List<string>
                    {
                        $"Grefur_{i}",
                        $"Grefur_{i}"
                    },
                    LogSubscription = SubscriptionLevel.Normal,
                    AlarmSubscription = AlarmLevel.None,
                    NotificationTypes = NotificationTypes.None
                };

                CustomerList.Add(NewCustomer);
            }

            return CustomerList;
        }
    }
}