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

public class ChangeCustomerDataEngine : IEventHandler<SystemReadyEvent>, IEventHandler<CustomerDataChangedEvent>
{
    private readonly ILogger<ChangeCustomerDataEngine> _logger;
    private readonly EventBus _eventBus;
    private readonly CustomerService _customerService;

    public ChangeCustomerDataEngine(
        ILogger<ChangeCustomerDataEngine> Logger,
        EventBus EventBus,
        CustomerService CustomerService)
    {
        _logger = Logger;
        _eventBus = EventBus;
        _customerService = CustomerService;

        _eventBus.Subscribe<SystemReadyEvent>(this);
        _eventBus.Subscribe<CustomerDataChangedEvent>(this);
    }

    public Task Handle(SystemReadyEvent Evt)
    {
        _logger.LogInformation("[ChangeCustomerDataEngine]: System ready, monitoring customer changes...");
        return Task.CompletedTask;
    }

    public async Task Handle(CustomerDataChangedEvent Evt)
    {
        // Vi ignorerer eventer som allerede er ferdigstilt for å unngå evig løkke
        if (Evt.Status != CustomerDataChangedEvent.EventStatus.Pending) return;

        _logger.LogInformation(
            "[ChangeCustomerDataEngine]: Handling {Action} for customer {CustomerId}",
            Evt.Action,
            Evt.Customer.CustomerId
        );

        try
        {
            switch (Evt.Action)
            {
                case CustomerDataChangedEvent.CustomerAction.Create:
                    await _customerService.CreateCustomerAsync(Evt.Customer);
                    Evt.Status = CustomerDataChangedEvent.EventStatus.Created;
                    break;

                case CustomerDataChangedEvent.CustomerAction.Edit:
                    await _customerService.EditCustomerAsync(Evt.Customer);
                    Evt.Status = CustomerDataChangedEvent.EventStatus.Updated;
                    break;

                case CustomerDataChangedEvent.CustomerAction.Delete:
                    await _customerService.DeleteCustomerAsync(Evt.Customer.CustomerId);
                    Evt.Status = CustomerDataChangedEvent.EventStatus.Deleted;
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported action {Evt.Action}");
            }

            _logger.LogInformation(
                "[ChangeCustomerDataEngine]: {Action} completed for {CustomerId}",
                Evt.Action,
                Evt.Customer.CustomerId
            );

            // Publiserer samme event tilbake med oppdatert status
            await _eventBus.Publish(Evt);
        }
        catch (Exception Ex)
        {
            Evt.Status = CustomerDataChangedEvent.EventStatus.Failed;

            await _eventBus.Publish(new ErrorEvent(
                errorCode: "CUSTOMER_ENGINE_FAILURE",
                level: ErrorLevel.ServiceBreach,
                message: $"Failed to {Evt.Action} customer {Evt.Customer.CustomerId}",
                source: nameof(ChangeCustomerDataEngine),
                correlationId: Evt.CorrelationId,
                exceptionDetails: Ex.ToString()
            ));

            // Vi sender også det opprinnelige eventet tilbake med status Failed
            await _eventBus.Publish(Evt);
        }
    }
}