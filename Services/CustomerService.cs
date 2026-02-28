using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using grefurBackend.Models;
using grefurBackend.Context;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using grefurBackend.Events.Queries;
using grefurBackend.Events;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using grefurBackend.Events.Lifecycle;
using Microsoft.AspNetCore.Identity;

namespace grefurBackend.Services
{
    public class CustomerService :
        IEventHandler<CustomerQueryEvent>,
        IEventHandler<RequestCustomerValueEnrichmentEvent>,
        IEventHandler<SystemReadyEvent>
    {
        private readonly ILogger<CustomerService> _logger;
        private readonly EventBus _eventBus;
        private readonly IDbContextFactory<MySqlContext> _mySqlContextFactory;
        private readonly PasswordHasher<GrefurCustomer> _passwordHasher = new();
        private readonly UserService _userService;

        public CustomerService(
            ILogger<CustomerService> Logger,
            EventBus EventBus,
            IDbContextFactory<MySqlContext> MySqlContextFactory,
            UserService UserService
            )
        {
            _logger = Logger;
            _eventBus = EventBus;
            _mySqlContextFactory = MySqlContextFactory;
            _userService = UserService;

            _eventBus.Subscribe<CustomerQueryEvent>(this);
            _eventBus.Subscribe<RequestCustomerValueEnrichmentEvent>(this);
            _eventBus.Subscribe<SystemReadyEvent>(this);

        }



        public async Task<bool> CreateCustomerAsync(GrefurCustomer customer)
        {
            using var context = await _mySqlContextFactory.CreateDbContextAsync();

            // {Check if customer or organization number already exists}
            var exists = await context.GrefurCustomers
                .AnyAsync(c => c.CustomerId == customer.CustomerId || c.OrganizationNumber == customer.OrganizationNumber);

            if (exists) return false;

            // {1. Create the customer record first (pure organization data)}
            customer.CreatedAt = DateTime.UtcNow;
            customer.IsEnabled = true;

            context.GrefurCustomers.Add(customer);
            await context.SaveChangesAsync();

            // {2. Create the separate admin user record via UserService}
            var initialUser = new GrefurUser
            {
                UserId = Guid.NewGuid().ToString(),
                Email = customer.EmailOfOrganization,
                PasswordHash = "admin", // {UserService hashes this}
                CustomerId = customer.CustomerId,
                Role = UserRole.Admin,
                ForcePasswordChange = true, // {Moved here to the User model}
                CreatedAt = DateTime.UtcNow
            };

            await _userService.CreateUserAsync(initialUser);

            _logger.LogInformation("[Grefur]: New customer {Id} created with a separate admin user.", customer.CustomerId);
            return true;
        }




        public async Task<string?> DeleteCustomerAsync(string customerId)
        {
            try
            {
                using var context = await _mySqlContextFactory.CreateDbContextAsync();
                var customer = await context.GrefurCustomers
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId);

                if (customer == null) return "NOT_FOUND";

                // Business logic: Hindre sletting hvis kunden har aktive noder i produksjon
                var hasDevices = await context.GrefurDevices.AnyAsync(d => d.CustomerId == customerId);
                if (hasDevices) return "HAS_ACTIVE_DEVICES";

                context.GrefurCustomers.Remove(customer);
                await context.SaveChangesAsync();

                _logger.LogWarning("Customer {Id} and all associated data purged from Grefur", customerId);

                // await _eventBus.Publish(new CustomerDeletedEvent(customerId));

                return "SUCCESS";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer {CustomerId}", customerId);
                return "ERROR";
            }
        }

        public async Task<(bool Success, GrefurCustomer? Customer)> EditCustomerAsync(GrefurCustomer config)
        {
            try
            {
                using var context = await _mySqlContextFactory.CreateDbContextAsync();
                var existing = await context.GrefurCustomers
                    .FirstOrDefaultAsync(c => c.CustomerId == config.CustomerId)
                    .ConfigureAwait(false);

                if (existing == null) return (false, null);

                // {1. Apply changes dynamically from the incoming config to the existing entity}
                context.Entry(existing).CurrentValues.SetValues(config);

                // {2. Check for changes using the ChangeTracker before saving}
                var entry = context.Entry(existing);

                // Capture change flags for events
                bool statusChanged = entry.Property(c => c.IsEnabled).IsModified;
                bool policiesChanged = entry.Property(c => c.LogSubscription).IsModified ||
                                       entry.Property(c => c.AlarmSubscription).IsModified;
                bool notifyChanged = entry.Property(c => c.NotificationSubscription).IsModified;

                // {3. Persist changes}
                if (entry.State == EntityState.Modified)
                {
                    await context.SaveChangesAsync().ConfigureAwait(false);

                    // --- Trigger Domain Events dynamically based on detected changes ---
                    if (statusChanged)
                    {
                        _logger.LogInformation("[Grefur]: Status changed for {CustomerId} to {Status}",
                            existing.CustomerId, existing.IsEnabled);
                    }

                    if (policiesChanged)
                    {
                        _logger.LogInformation("[Grefur]: Policies updated for {CustomerId}", existing.CustomerId);
                        // Her kan du også trigge et event til Brokeren din (EMQX) om nye rettigheter
                    }

                    if (notifyChanged)
                    {
                        _logger.LogInformation("[Grefur]: Notification preference updated for {CustomerId}", existing.CustomerId);
                    }
                }

                return (true, existing);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Grefur]: Error editing customer {CustomerId}", config.CustomerId);
                return (false, null);
            }
        }

        public async Task<List<GrefurCustomer>> GetAllCustomersAsync()
        {
            using var context = await _mySqlContextFactory.CreateDbContextAsync();
            return await context.GrefurCustomers.ToListAsync().ConfigureAwait(false);
        }

        public async Task<GrefurCustomer?> GetCustomerByIdAsync(string CustomerId)
        {
            using var context = await _mySqlContextFactory.CreateDbContextAsync();
            return await context.GrefurCustomers
                .FirstOrDefaultAsync(c => c.CustomerId == CustomerId)
                .ConfigureAwait(false);
        }

        public async Task<GrefurCustomer?> GetCustomerByDeviceIdAsync(string DeviceId)
        {
            using var context = await _mySqlContextFactory.CreateDbContextAsync();

            var device = await context.GrefurDevices
                .FirstOrDefaultAsync(d => d.DeviceId == DeviceId)
                .ConfigureAwait(false);

            if (device == null)
            {
                _logger.LogWarning("Device {DeviceId} not found in database.", DeviceId);
                return null;
            }

            return await context.GrefurCustomers
                .FirstOrDefaultAsync(c => c.CustomerId == device.CustomerId)
                .ConfigureAwait(false);
        }

        public async Task<List<GrefurCustomer>> GetAllActiveSubscribersAsync()
        {
            using var context = await _mySqlContextFactory.CreateDbContextAsync();

            // Vi flytter logikken fra IsActiveSubscriber() inn i Where-klausulen
            // slik at databasen gjør jobben.
            var activeCustomers = await context.GrefurCustomers
                .Where(c => c.IsEnabled &&
                           (c.LogSubscription != LogSubscriptionLevel.None ||
                            c.AlarmSubscription != AlarmLevel.None))
                .OrderByDescending(c => c.CreatedAt) // Sortering skjer i SQL
                .ToListAsync()
                .ConfigureAwait(false);

            _logger.LogInformation("Found {Count} active customers via optimized SQL query.", activeCustomers.Count);

            return activeCustomers;
        }

        public async Task Handle(CustomerQueryEvent Evt)
        {
            var customer = await GetCustomerByDeviceIdAsync(Evt.DeviceId).ConfigureAwait(false);
            if (customer != null)
            {
                var response = new CustomerQueryResponseEvent(
                    deviceId: Evt.DeviceId,
                    customerIdParam: customer.CustomerId,
                    source: nameof(CustomerService),
                    correlationId: Evt.CorrelationId
                );
                await _eventBus.Publish(response).ConfigureAwait(false);
            }
        }

        public async Task Handle(RequestCustomerValueEnrichmentEvent Evt)
        {
            _logger.LogDebug("[CustomerService]: Enriching data for device {DeviceId}", Evt.DeviceId);

            var customer = await GetCustomerByDeviceIdAsync(Evt.DeviceId).ConfigureAwait(false);

            if (customer == null)
            {
                _logger.LogWarning("[CustomerService]: No customer found for {DeviceId}.", Evt.DeviceId);
                var unknownValueEvent = new UnknownValueEvent(
                    Topic: Evt.Topic,
                    Source: nameof(CustomerService),
                    CorrelationId: Evt.CorrelationId
                );

                await _eventBus.Publish(unknownValueEvent).ConfigureAwait(false);

                return;
            }



            if (customer.AlarmSubscription == 0 && customer.LogSubscription == 0 /* NotificationSubscription*/)
            {
                _logger.LogWarning("[CustomerService]: No point of subscribing to {DeviceId}.", Evt.DeviceId);
                var unknownValueEvent = new UnknownValueEvent(
                    Topic: Evt.Topic,
                    Source: nameof(CustomerService),
                    CorrelationId: Evt.CorrelationId
                );

                await _eventBus.Publish(unknownValueEvent).ConfigureAwait(false);

                return;
            }

            var response = new ResponseCustomerValueEnrichmentEvent(
                Customer: customer,
                SubscriptionId: $"SUB-{customer.CustomerId}",
                AlarmPolicyLevel: customer.AlarmSubscription,
                LogPolicyLevel: customer.LogSubscription,
                Source: nameof(CustomerService),
                CorrelationId: Evt.CorrelationId,
                DeviceId: Evt.DeviceId,
                Topic: Evt.Topic,
                Value: Evt.Value,
                ValueType: Evt.ValueType
            );

            try
            {
                await _eventBus.Publish(response).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CustomerService]: Error publishing enrichment for {CustomerId}", customer.CustomerId);
            }
        }

        public async Task Handle(SystemReadyEvent Evt)
        {
            _logger.LogInformation("[CustomerService]: SystemReadyEvent received. Checking for system admin...");

            try
            {
                using var context = await _mySqlContextFactory.CreateDbContextAsync();

                var existingAdmin = await context.GrefurUsers
                    .FirstOrDefaultAsync(u =>
                        (u.Email == "admin@grefur.com" &&
                        u.Role == UserRole.SystemAdmin
                        ));


                if (existingAdmin == null)
                {
                    string defaultAdminPassword = "admin";
                    string passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultAdminPassword);

                    var systemAdmin = new GrefurUser
                    {
                        UserId = Guid.NewGuid().ToString(),
                        PasswordHash = passwordHash,
                        Email = "admin@grefur.com",
                        CustomerId = "GREFUR-INTERNAL",
                        Role = UserRole.SystemAdmin,
                        IsEnabled = true,
                        ForcePasswordChange = false,
                        CreatedAt = DateTime.UtcNow,
                        MetadataJson = "{\"note\": \"Initial system administrator\"}"
                    };

                    context.GrefurUsers.Add(systemAdmin);
                    await context.SaveChangesAsync();

                    _logger.LogInformation("[Seed]: SystemAdmin 'admin_admin' created successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Seed]: Failed to seed SystemAdmin during SystemReadyEvent.");
            }
        }

        public async Task<object?> GetCustomerServicesStatusAsync(string customerId)
        {
            var customer = await GetCustomerByIdAsync(customerId).ConfigureAwait(false);
            if (customer == null) return null;

            string DetermineStatus(Enum subscription) => Convert.ToInt32(subscription) == 0 ? "Inactive" : "Active";

            return new
            {
                customerId = customer.CustomerId,
                isEnabled = customer.IsEnabled,
                // Fikser CS0826 ved å bruke en eksplisitt liste i stedet for implicit array
                services = new List<object>
                {
                    new
                    {
                        serviceName = "LoggerService",
                        status = DetermineStatus(customer.LogSubscription),
                        level = customer.LogSubscription.ToString()
                    },
                    new
                    {
                        serviceName = "AlarmService",
                        status = DetermineStatus(customer.AlarmSubscription),
                        level = customer.AlarmSubscription.ToString()
                    },
                    new
                    {
                        serviceName = "NotificationService",
                        status = DetermineStatus(customer.NotificationSubscription),
                        type = customer.NotificationSubscription.ToString()
                    }
                },
                timestamp = DateTime.UtcNow
            };
        }

        public async Task<object?> GetCustomerMetadataAsync(string customerId)
        {
            var customer = await GetCustomerByIdAsync(customerId).ConfigureAwait(false);
            if (customer == null) return null;

            return new
            {
                organization = new
                {
                    id = customer.CustomerId,
                    name = customer.OrganizationName,
                    email = customer.EmailOfOrganization,
                    memberSince = customer.CreatedAt
                }
                
            };
        }

    }
}