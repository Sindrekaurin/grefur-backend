using System;
using grefurBackend.Types;

namespace grefurBackend.Events;

public class BrokerConnectionEvent : Event
{
    public BrokerStatus Status { get; }
    public string BrokerAddress { get; }
    public string Message { get; }

    public BrokerConnectionEvent(
        BrokerStatus status,
        string brokerAddress,
        string message,
        string source,
        string correlationId)
        : base(
            correlationId,
            source,
            nameof(BrokerConnectionEvent),
            new { status, brokerAddress }) // Sender med et anonymt objekt som 'payload'
    {
        Status = status;
        BrokerAddress = brokerAddress;
        Message = message;
    }
}