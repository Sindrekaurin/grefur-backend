using System;
using System.Collections.Generic;
using grefurBackend.Events;
using grefurBackend.Models;

namespace grefurBackend.Events
{
    public class CustomerDataChangedEvent : Event
    {
        public enum CustomerAction
        {
            Create,
            Edit,
            Delete
        }

        public enum EventStatus
        {
            Pending,
            Created,
            Updated,
            Deleted,
            Failed
        }

        public CustomerAction Action { get; set; }
        public EventStatus Status { get; set; } = EventStatus.Pending;
        public GrefurCustomer Customer { get; }

        public CustomerDataChangedEvent(
            CustomerAction Action,
            GrefurCustomer Customer,
            string Source,
            string CorrelationId)
            : base(
                eventType: "CustomerDataChanged",
                source: Source,
                correlationId: CorrelationId,
                payload: new { Action, Customer, Status = EventStatus.Pending })
        {
            this.Action = Action;
            this.Customer = Customer;
        }
    }
}