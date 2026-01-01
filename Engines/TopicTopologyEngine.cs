using System;
using System.Collections.Concurrent; // Nødvendig for trådsikkerhet
using System.Collections.Generic;
using System.Threading.Tasks;
using grefurBackend.Events;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using grefurBackend.Events.Integration;
using Microsoft.Extensions.Logging;
using grefurBackend.Events.Queries;
using grefurBackend.Services;
using grefurBackend.Models;

namespace grefurBackend.Engines;

public class TopicTopologyEngine : 
    IEventHandler<DeviceRegisteredEvent>,
    IEventHandler<MqttMessageReceivedEvent>
{
    private readonly EventBus _eventBus;
    private readonly MqttService _mqttService;
    private readonly CacheService _cacheService;
    private readonly ILogger<TopicTopologyEngine> _logger;

    // Bruker ConcurrentDictionary for å unngå InvalidOperationException ved flertrådet tilgang
    private readonly ConcurrentDictionary<string, string> _deviceBaseTopics = new();

    private readonly Dictionary<string, string> _avoidTopicSuffixes = new()
    {
        { "/debug", "Internal hardware debugging" },
        { "/raw", "Unprocessed sensor data" },
        { "/config", "Configuration updates only" },
        { "/info/baseTopic", "Not value, basetopic, other handler" }
    };

    private readonly Dictionary<string, string> _avoidTopicPrefixes = new()
    {
        { "test/", "Development testing sensors" },
        { "internal/", "Internal Grefur monitoring" }
    };

    public TopicTopologyEngine(
        EventBus eventBus,
        CacheService cacheService,
        MqttService mqttService, 
        ILogger<TopicTopologyEngine> logger
        )
    {
        _eventBus = eventBus;
        _cacheService = cacheService;
        _mqttService = mqttService;
        _logger = logger;
        

        _eventBus.Subscribe<DeviceRegisteredEvent>(this);
        _eventBus.Subscribe<MqttMessageReceivedEvent>(this);
    }

    public async Task Handle(DeviceRegisteredEvent evt)
    {
        _logger.LogInformation("[TopicTopologyEngine]: DeviceRegisteredEvent received: {DeviceId}", evt.DeviceId);

        var baseTopic = $"{evt.DeviceId}/info/baseTopic";
        if (_mqttService.IsConnected)
        {
            _mqttService.Subscribe(baseTopic);
            _logger.LogInformation("[TopicTopologyEngine]: Subscribed to base topic {Topic}", baseTopic);
        }

        var topicBoundEvent = new TopicBoundEvent(
            customerId: evt.CustomerId,
            deviceId: evt.DeviceId,
            baseTopic: baseTopic,
            source: nameof(TopicTopologyEngine),
            correlationId: evt.CorrelationId,
            status: TopicBoundStatus.Success
        );

        await _eventBus.Publish(topicBoundEvent).ConfigureAwait(false);
    }

    /*public async Task<string?> queryCustomerIdAsync(string deviceId)
    {
        var queryEvent = new CustomerQueryEvent(
            deviceId: deviceId,
            source: nameof(TopicTopologyEngine),
            correlationId: Guid.NewGuid().ToString()
        );

        var response = await _eventBus.RequestAsync<CustomerQueryEvent, CustomerQueryResponseEvent>(
            queryEvent,
            r => r.DeviceId == deviceId
            //timeoutMs: 5000
        );

        return response?.CustomerId;
    }*/

    public async Task Handle(MqttMessageReceivedEvent e)
    {
        
        try
        {
            // Bruker PascalCase properties fra MqttMessageReceivedEvent
            if (e.DeviceId == null || e.RawPayload == null) return;

            var payloadString = e.RawPayload.Trim();
            var eventDeviceId = e.DeviceId ?? "unknown";

            
            var eventTopic = e.Topic;

            var isInformationalMessage = _avoidTopicPrefixes.Keys.Any(prefix => eventTopic.StartsWith(prefix)) ||
                                            _avoidTopicSuffixes.Keys.Any(suffix => eventTopic.EndsWith(suffix));

            if (!isInformationalMessage)
            {
                try
                {
                    // Bruker PascalCase i konstruktøren til ValueReceivedEvent
                    var valueEvent = new ValueReceivedEvent(
                        Source: nameof(TopicTopologyEngine),
                        CorrelationId: e.CorrelationId,
                        DeviceId: eventDeviceId,
                        Topic: eventTopic,
                        Value: e.RawPayload ?? string.Empty
                        //Customer: customer
                    );

                    await _eventBus.Publish(valueEvent).ConfigureAwait(false);

                    _logger.LogDebug("[TopicTopologyEngine]: Published ValueReceivedEvent for {Topic}",
                        eventTopic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TopicTopologyEngine]: Error handling MQTT message while publishing ValueReceivedEvent from {DeviceId}", e.DeviceId);
                }
            }
            else // If basetopic or development topic
            {
                try
                {
                    _logger.LogInformation("[TopicTopologyEngine]: BaseTopic for {DeviceId} registered as {BaseTopic}",
                        eventDeviceId, payloadString);

                    var sensorTopicPattern = $"{eventDeviceId}/{payloadString}/#";

                    // Antar at MqttService har PascalCase på IsConnected/IsConnected basert på din stil
                    if (_mqttService.IsConnected)
                    {
                        _mqttService.Subscribe(sensorTopicPattern);

                        _deviceBaseTopics.AddOrUpdate(eventDeviceId, payloadString, (key, oldValue) => payloadString);

                        _logger.LogInformation("[TopicTopologyEngine]: Subscribed to sensor topics {SensorTopicPattern}", sensorTopicPattern);

                        // Bruker PascalCase for TopicBoundStatus.Success
                        var topicBoundEvent = new TopicBoundEvent(
                            customerId: "unknown",
                            deviceId: eventDeviceId,
                            baseTopic: payloadString,
                            status: TopicBoundStatus.Success,
                            source: nameof(TopicTopologyEngine),
                            correlationId: Guid.NewGuid().ToString()
                        );

                        _logger.LogInformation("[TopicTopologyEngine]: Published TopicBoundEvent");

                        await _eventBus.Publish(topicBoundEvent).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogError("[TopicTopologyEngine]: Broker is offline - Could not subscribe. TopicBoundEvent was not created");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TopicTopologyEngine]: Error handling base topic subscription");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TopicTopologyEngine]: Error in handle MqttMessageReceivedEvent");
        }
    }
}