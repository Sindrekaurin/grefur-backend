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
using grefurBackend.Events.Device;
using grefurBackend.Helpers;

namespace grefurBackend.Engines;

public class TopicTopologyEngine : 
    IEventHandler<DeviceRegisteredEvent>,
    IEventHandler<MqttMessageReceivedEvent>,
    IEventHandler<UnknownValueEvent>
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
        _eventBus.Subscribe<UnknownValueEvent>(this);
        
    }

    public async Task Handle(DeviceRegisteredEvent evt)
    {
        _logger.LogInformation("[TopicTopologyEngine]: DeviceRegisteredEvent received: {DeviceId}", evt.DeviceId);

        var baseTopic = TopicHelper.ConstructTopic(evt.DeviceId, "info/baseTopic");

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

    public async Task Handle(UnknownValueEvent Evt)
    {
        try
        {
            if (string.IsNullOrEmpty(Evt.Topic)) return;

            string deviceId = Evt.Topic.Split('/')[0];
            string baseTopicFound = "unknown";

            if (_mqttService.IsConnected)
            {
                _mqttService.Unsubscribe(Evt.Topic);

                // Forsøker å hente og fjerne basetopic fra cache
                if (_deviceBaseTopics.TryRemove(deviceId, out var baseTopic))
                {
                    baseTopicFound = baseTopic;
                    var fullSensorPattern = TopicHelper.GetDeviceWildcard(deviceId, baseTopicFound);
                    _mqttService.Unsubscribe(fullSensorPattern);

                    _logger.LogDebug("[TopicTopologyEngine]: Unsubscribed from baseTopic pattern: {Pattern}", fullSensorPattern);
                }

                // Fullstendig purging av enheten
                _mqttService.Unsubscribe($"{deviceId}/#");
                _mqttService.Unsubscribe($"{deviceId}/info/baseTopic");

                _logger.LogDebug("[TopicTopologyEngine]: Fully purged MQTT subscriptions for device {DeviceId}", deviceId);

                var removedEvent = new TopicBoundRemovedEvent(
                    customerId: "unknown", // UnknownValueEvent har ikke kunde-info
                    deviceId: deviceId,
                    baseTopic: baseTopicFound,
                    status: TopicBoundStatus.Success,
                    source: nameof(TopicTopologyEngine),
                    correlationId: Evt.CorrelationId,
                    statusMessage: "Successfully cleaned up topology after unknown value detection"
                );

                await _eventBus.Publish(removedEvent).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("[TopicTopologyEngine]: Broker offline, could not unsubscribe from {Topic}", Evt.Topic);

                var errorEvent = new ErrorEvent(
                    errorCode: "TOPOLOGY_CLEANUP_OFFLINE",
                    level: ErrorLevel.Critical,
                    message: $"Failed to cleanup MQTT topology for device {deviceId} - Broker offline",
                    source: nameof(TopicTopologyEngine),
                    correlationId: Evt.CorrelationId,
                    exceptionDetails: "MqttService.IsConnected was false during cleanup"
                );

                await _eventBus.Publish(errorEvent).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TopicTopologyEngine]: Error handling UnknownValueEvent for topic {Topic}", Evt.Topic);

            var errorEvent = new ErrorEvent(
                errorCode: "TOPOLOGY_CLEANUP_ERROR",
                level: ErrorLevel.Critical,
                message: "Internal error during topology cleanup",
                source: nameof(TopicTopologyEngine),
                correlationId: Evt.CorrelationId,
                exceptionDetails: ex.ToString()
            );

            await _eventBus.Publish(errorEvent).ConfigureAwait(false);
        }
    }

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

                    var sensorTopicPattern = TopicHelper.GetDeviceWildcard(eventDeviceId, payloadString);

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