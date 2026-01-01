// CustomerQuery.cs

using System;
using System.Threading.Tasks;
using grefurBackend.Events;        // for Event
using grefurBackend.Infrastructure; // for IEventHandler<>
using grefurBackend.Services;

namespace grefurBackend.Events.Queries
{
    public sealed class CustomerQueryEvent : Event
    {
        public string DeviceId { get; }
        public CustomerQueryEvent(string deviceId, string source, string correlationId)
            : base("CustomerQuery", source, correlationId, payload: new { deviceId })
        {
            DeviceId = deviceId;
        }
    }




    public sealed class CustomerQueryResponseEvent : Event
    {
        public string DeviceId { get; }
        public string CustomerId { get; }  // rename to PascalCase

        public CustomerQueryResponseEvent(string deviceId, string customerIdParam, string source, string correlationId)
            : base("CustomerQueryResponse", source, correlationId, payload: new { deviceId, customerId = customerIdParam })
        {
            DeviceId = deviceId;
            CustomerId = customerIdParam; // now no conflict
        }
    }

}
