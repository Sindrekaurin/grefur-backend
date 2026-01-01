using grefurBackend.Models;

namespace grefurBackend.Events.Domain;

public sealed class CustomerLoadedEvent : Event
{
    public GrefurCustomer Customer { get; }

    // Helper property to fix CS1061 errors in engines
    public string CustomerId => Customer?.CustomerId ?? "unknown";

    public CustomerLoadedEvent(GrefurCustomer Customer, string Source, string CorrelationId)
        : base(
            eventType: "CustomerLoaded",
            source: Source,
            correlationId: CorrelationId,
            payload: Customer)
    {
        this.Customer = Customer;
    }
}