using System;
using grefurBackend.Events;
using grefurBackend.Models;

namespace grefurBackend.Events
{
    public class ChangeCustomerDataEvent : Event
    {
        public enum CustomerAction
        {
            Create,
            Edit,
            Delete
        }

        public CustomerAction Action { get; set; }
        public GrefurCustomer Customer { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public ChangeCustomerDataEvent(
            CustomerAction Action,      // Endret til PascalFormat for å matche kallet ditt
            GrefurCustomer Customer,    // Endret til PascalFormat for å matche kallet ditt
            string Source,
            string correlationId)
            : base(
                eventType: "ChangeCustomerData",
                source: Source,
                correlationId: correlationId,
                payload: new { Action, Customer, timestamp = DateTime.UtcNow })
        {
            this.Action = Action;
            this.Customer = Customer;
        }
    }
}