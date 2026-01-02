using System;
using System.Threading.Tasks;
using grefurBackend.Events;
using grefurBackend.Models;
using grefurBackend.Events.Domain;
using grefurBackend.Events.Lifecycle;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;

namespace grefurBackend.Engines;

public class ChangeCustomerDataEngine : IEventHandler<SystemReadyEvent>, IEventHandler<ChangeCustomerDataEvent>
{
    private readonly ILogger<ChangeCustomerDataEngine> _logger;
    private readonly EventBus _eventBus;
    private readonly CustomerService _customerService;

    public ChangeCustomerDataEngine(
        ILogger<ChangeCustomerDataEngine> Logger,
        CustomerService CustomerService,
        EventBus EventBus)
    {
        _logger = Logger;
        _customerService = CustomerService;
        _eventBus = EventBus;

        _eventBus.Subscribe<SystemReadyEvent>(this);
        _eventBus.Subscribe<ChangeCustomerDataEvent>(this);
    }

    public Task Handle(SystemReadyEvent Evt)
    {
        _logger.LogInformation("[ChangeCustomerDataEngine]: System ready, monitoring customer changes...");
        return Task.CompletedTask;
    }

    public async Task Handle(ChangeCustomerDataEvent Evt)
    {
        _logger.LogInformation("[ChangeCustomerDataEngine]: Handling {Action} for customer {CustomerId}",
            Evt.Action, Evt.Customer.CustomerId);

        try
        {
            bool Success = false;

            if (Evt.Action == ChangeCustomerDataEvent.CustomerAction.Create)
            {
                Success = await _customerService.CreateCustomer(Evt.Customer).ConfigureAwait(false);
            }
            else if (Evt.Action == ChangeCustomerDataEvent.CustomerAction.Edit)
            {
                Success = await _customerService.EditCustomer(Evt.Customer).ConfigureAwait(false);
            }
            else if (Evt.Action == ChangeCustomerDataEvent.CustomerAction.Delete)
            {
                Success = await _customerService.DeleteCustomer(Evt.Customer.CustomerId).ConfigureAwait(false);
            }

            if (!Success)
            {
                throw new Exception($"Service layer returned failure for action {Evt.Action}");
            }

            _logger.LogInformation("[ChangeCustomerDataEngine]: {Action} completed for {CustomerId}",
                Evt.Action, Evt.Customer.CustomerId);
        }
        catch (Exception Ex)
        {
            await _eventBus.Publish(new ErrorEvent(
                errorCode: "CUSTOMER_ENGINE_FAILURE",
                level: ErrorLevel.ServiceBreach,
                message: $"Failed to {Evt.Action} customer {Evt.Customer.CustomerId}",
                source: nameof(ChangeCustomerDataEngine),
                correlationId: Evt.CorrelationId,
                exceptionDetails: Ex.ToString()
            )).ConfigureAwait(false);
        }
    }
}