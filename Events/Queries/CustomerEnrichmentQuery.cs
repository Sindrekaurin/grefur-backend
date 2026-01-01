using System;
using grefurBackend.Events;
using grefurBackend.Models;

namespace grefurBackend.Events.Queries;

public sealed class RequestCustomerValueEnrichmentEvent : Event
{
    public string DeviceId { get; }
    public string Topic { get; }
    public string Value { get; }
    public string ValueType { get; }
    public string CorrelationId { get; }

    public RequestCustomerValueEnrichmentEvent(
        string DeviceId,
        string Topic,
        string Value,
        string ValueType,
        string Source,
        string CorrelationId)
        : base(
            "RequestCustomerValueEnrichment",
            Source,
            CorrelationId,
            payload: new
            {
                DeviceId = DeviceId,
                Topic = Topic,
                Value = Value,
                ValueType = ValueType
            })
    {
        this.DeviceId = DeviceId;
        this.Topic = Topic;
        this.Value = Value;
        this.ValueType = ValueType;
        this.CorrelationId = CorrelationId;
    }
}

public sealed class ResponseCustomerValueEnrichmentEvent : Event
{
    public GrefurCustomer Customer { get; }
    public string SubscriptionId { get; }
    public AlarmLevel AlarmPolicyLevel { get; }
    public SubscriptionLevel LogPolicyLevel { get; }
    public string DeviceId { get; }
    public string Topic { get; }
    public string Value { get; }
    public string ValueType { get; }
    public string CorrelationId { get; }

    public ResponseCustomerValueEnrichmentEvent(
        GrefurCustomer Customer,
        string SubscriptionId,
        AlarmLevel AlarmPolicyLevel,
        SubscriptionLevel LogPolicyLevel,
        string DeviceId,
        string Topic,
        string Value,
        string ValueType,
        string Source,
        string CorrelationId)
        : base(
            "ResponseCustomerValueEnrichment",
            Source,
            CorrelationId,
            payload: new
            {
                CustomerId = Customer.CustomerId,
                SubscriptionId = SubscriptionId,
                AlarmPolicyLevel = AlarmPolicyLevel,
                LogPolicyLevel = LogPolicyLevel,
                DeviceId = DeviceId,
                Topic = Topic,
                Value = Value,
                ValueType = ValueType
            })
    {
        this.Customer = Customer;
        this.SubscriptionId = SubscriptionId;
        this.AlarmPolicyLevel = AlarmPolicyLevel;
        this.LogPolicyLevel = LogPolicyLevel;
        this.DeviceId = DeviceId;
        this.Topic = Topic;
        this.Value = Value;
        this.ValueType = ValueType;
        this.CorrelationId = CorrelationId;
    }
}