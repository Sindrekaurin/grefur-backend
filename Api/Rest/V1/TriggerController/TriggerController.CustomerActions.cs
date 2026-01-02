using grefurBackend.Events;
using grefurBackend.Models;

namespace grefurBackend.Api.Rest.V1
{
    public partial class TriggerController
    {
        private async Task<object> HandleCustomerChangeAsync(string correlationId)
        {
            var dummyCustomer = new GrefurCustomer
            {
                CustomerId = "CUST-001",
                RegisteredDevices = new List<string> { "Grefur_3461", "Grefur_235cfe" },
                LogSubscription = SubscriptionLevel.Normal,
                AlarmSubscription = AlarmLevel.None,
                NotificationTypes = NotificationTypes.None
            };

            _logger.LogInformation("TriggerController: Publishing ChangeCustomerDataEvent for {CustomerId}", dummyCustomer.CustomerId);

            var customerEvent = new ChangeCustomerDataEvent(
                Action: ChangeCustomerDataEvent.CustomerAction.Create,
                Customer: dummyCustomer,
                Source: nameof(TriggerController),
                correlationId
            );

            await _eventBus.Publish(customerEvent).ConfigureAwait(false);

            _logger.LogInformation("TriggerController: Published ChangeCustomerDataEvent for {CustomerId}", dummyCustomer.CustomerId);

            var response = new
            {
                Event = "ChangeCustomerData",
                Status = "Triggered",
                CustomerId = dummyCustomer.CustomerId,
                CorrelationId = correlationId
            };

            _logger.LogInformation("TriggerController: Returning {resp}", response);

            return response;
        }
    }
}