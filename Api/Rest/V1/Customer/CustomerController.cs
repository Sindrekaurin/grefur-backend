// CustomerController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using grefurBackend.Context;
using grefurBackend.Models;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using grefurBackend.Helpers;
using grefurBackend.Services;
using grefurBackend.Infrastructure;
using grefurBackend.Events;
using grefurBackend.Events.Domain;
using grefurBackend.Events.Lifecycle;

namespace grefurBackend.Controllers.Api.Rest.V1.Customers;

[ApiController]
[Route("api/rest/v1/customers")]
[Authorize]
public class CustomerController : ControllerBase
{
    private readonly CustomerService _customerService;
    private readonly ILogger<CustomerController> _logger;
    private readonly IDbContextFactory<MySqlContext> _contextFactory;
    private readonly EventBus _eventBus;
    private const string AllGrefurRoles = nameof(UserRole.SystemAdmin) + "," +
                                     nameof(UserRole.Admin) + "," +
                                     nameof(UserRole.User);

    public CustomerController(
        CustomerService customerService,
        ILogger<CustomerController> logger,
        IDbContextFactory<MySqlContext> contextFactory,
        EventBus eventBus
    )
    {
        _customerService = customerService;
        _logger = logger;
        _contextFactory = contextFactory;
        _eventBus = eventBus;
    }

    [HttpGet]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> GetAllCustomers()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var customers = await context.GrefurCustomers
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("SystemAdmin retrieved all customers. Count: {Count}", customers.Count);

        return Ok(customers);
    }


    [HttpPost("add")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> AddCustomer([FromBody] GrefurCustomer? newCustomer)
    {
        if (newCustomer == null) return BadRequest();

        var success = await _customerService.CreateCustomerAsync(newCustomer);
        if (!success) return Conflict();

        // {Nå vet kompilatoren at newCustomer ikke er null}
        var customerLoadedEvent = new CustomerLoadedEvent(
            Customer: newCustomer!,
            Source: nameof(CustomerController),
            CorrelationId: Guid.NewGuid().ToString()
        );

        await _eventBus.Publish(customerLoadedEvent);
        return Ok();
    }

    [HttpDelete("remove/{customerId}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> RemoveCustomer(string customerId)
    {
        var result = await _customerService.DeleteCustomerAsync(customerId);

        return result switch
        {
            "SUCCESS" => Ok(new { success = true }),
            "NOT_FOUND" => NotFound(new { message = "Customer not found" }),
            "HAS_ACTIVE_DEVICES" => BadRequest(new { message = "Cannot remove customer with active devices. Remove devices first." }),
            _ => StatusCode(500, new { message = "An internal error occurred" })
        };
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var customerId = User.FindFirst("customerId")?.Value;

        if (string.IsNullOrEmpty(customerId))
        {
            return Unauthorized(new { message = "Customer ID not found in token" });
        }

        using var context = await _contextFactory.CreateDbContextAsync();

        var customer = await context.GrefurCustomers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null || !customer.IsEnabled)
        {
            return StatusCode(403, new { message = "Organisasjonen er deaktivert eller finnes ikke." });
        }

        return Ok(new
        {
            organizationName = customer.OrganizationName,
            logSubscription = (int)customer.LogSubscription,
            alarmSubscription = (int)customer.AlarmSubscription,
            notificationSubscription = (int)customer.NotificationSubscription,
            isEnabled = customer.IsEnabled,
            createdAt = customer.CreatedAt,

            // --- Usage Statistics ---
            usage = new
            {
                logging = new
                {
                    thisMonth = customer.LoggedPointsUsage.ThisMonth,
                    lastMonth = customer.LoggedPointsUsage.LastMonth,
                    total = customer.LoggedPointsUsage.Total
                },
                notifications = new
                {
                    sms = new
                    {
                        thisMonth = customer.NotificationUsage.Sms.ThisMonth,
                        lastMonth = customer.NotificationUsage.Sms.LastMonth,
                        total = customer.NotificationUsage.Sms.Total
                    },
                    email = new
                    {
                        thisMonth = customer.NotificationUsage.Email.ThisMonth,
                        lastMonth = customer.NotificationUsage.Email.LastMonth,
                        total = customer.NotificationUsage.Email.Total
                    }
                }
            }
        });
    }

    [HttpPost("update-notifications")]
    public async Task<IActionResult> UpdateNotifications([FromBody] int notificationType)
    {
        var customerId = User.FindFirst("customerId")?.Value;
        using var context = await _contextFactory.CreateDbContextAsync();

        var customer = await context.GrefurCustomers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null || !customer.IsEnabled)
        {
            return StatusCode(403, new { message = "Organisasjonen er deaktivert eller finnes ikke." });
        }

        customer.NotificationSubscription = (NotificationTypes)notificationType;
        await context.SaveChangesAsync();

        _logger.LogInformation("Customer {Id} updated notification settings to {Type}", customerId, customer.NotificationSubscription);

        return Ok(new { success = true });
    }

    [HttpGet("types")]
    [Authorize(Roles = "SystemAdmin")] // Ofte greit at bare admin ser alle mulige konfigurasjoner
    public IActionResult GetPossibleTypes()
    {
        Console.WriteLine("Recived MetaData request");
        return Ok(new
        {
            loggingLevels = EnumHelper.GetEnumMetadata<LogSubscriptionLevel>(),
            alarmLevels = EnumHelper.GetEnumMetadata<AlarmLevel>(),
            notificationTypes = EnumHelper.GetEnumMetadata<NotificationTypes>()
        });
    }


    [HttpPut("update/{customerId}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> UpdateCustomer(string customerId, [FromBody] GrefurCustomer updatedData)
    {
        if (updatedData == null || customerId != updatedData.CustomerId)
        {
            return BadRequest(new { message = "Mismatched or invalid customer data" });
        }

        var (success, customer) = await _customerService.EditCustomerAsync(updatedData).ConfigureAwait(false);

        // Check both success and that the customer object is actually returned
        if (!success || customer == null)
        {
            return NotFound(new { message = "Customer not found or update failed" });
        }

        /* Trigger the Load event for cache and broker updates */
        var customerLoadedEvent = new CustomerLoadedEvent(
            Customer: customer, // Changed 'customer:' to 'Customer:'
            Source: nameof(CustomerController),
            CorrelationId: Guid.NewGuid().ToString()
        );

        await _eventBus.Publish(customerLoadedEvent).ConfigureAwait(false);

        _logger.LogInformation("[CustomerController]: Customer {Id} updated and load event published.", customer.CustomerId);

        return Ok(new { success = true, message = "Customer updated successfully through Grefur Service" });
    }



    [HttpGet("services-status")]
    [Authorize(Roles = AllGrefurRoles)]
    public async Task<IActionResult> GetServicesStatus()
    {
        var customerId = User.FindFirst("customerId")?.Value;
        if (string.IsNullOrEmpty(customerId)) return Unauthorized();

        var status = await _customerService.GetCustomerServicesStatusAsync(customerId);
        return status != null ? Ok(status) : NotFound();
    }

    [HttpGet("metadata")]
    [Authorize(Roles = AllGrefurRoles)]
    public async Task<IActionResult> GetMetadata()
    {
        var customerId = User.FindFirst("customerId")?.Value;
        if (string.IsNullOrEmpty(customerId)) return Unauthorized();

        var metadata = await _customerService.GetCustomerMetadataAsync(customerId);
        return metadata != null ? Ok(metadata) : NotFound();
    }

    [HttpGet("model/metadata")]
    [AllowAnonymous]
    public IActionResult GetDeviceMetadata()
    {
        var type = typeof(GrefurCustomer);
        var metaAttr = type.GetCustomAttribute<MetaDataAttribute>();

        if (metaAttr == null)
        {
            return Ok(new
            {
                singleName = type.Name,
                verboseName = type.Name + "s",
                description = ""
            });
        }

        var metadata = new
        {
            singleName = metaAttr.SingleName,
            verboseName = metaAttr.VerboseName,
            description = metaAttr.Description
        };

        return Ok(metadata);
    }




}