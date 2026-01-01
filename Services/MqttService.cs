using System;
using System.Text;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Microsoft.Extensions.Logging;
using grefurBackend.Events;
using grefurBackend.Events.Integration;
using grefurBackend.Infrastructure;
using System.Net.NetworkInformation; // Ping
using grefurBackend.Types; // BrokerStatus

namespace grefurBackend.Services;

public class MqttSettings
{
    public string username { get; set; } = "DATALAKE";
    public string password { get; set; } = "DATALAKE2024";
    public string broker { get; set; } = "192.168.10.183";
    public int port { get; set; } = 1883;
    public string clientId { get; set; } = "grefurBackendServer";
    public bool isDebug { get; set; } = true;
}

public class MqttService
{
    private MqttClient? _client;
    private readonly ILogger<MqttService> _logger;
    private readonly MqttSettings _settings;
    private readonly EventBus _eventBus;

    public event EventHandler<MqttMessageReceivedEvent>? OnMessageReceived;

    public MqttService(
        ILogger<MqttService> logger,
        MqttSettings settings,
        EventBus eventBus)
    {
        _logger = logger;
        _settings = settings;
        _eventBus = eventBus;
    }

    public async Task Connect()
    {
        try
        {
            _client = new MqttClient(
                _settings.broker,
                _settings.port,
                false,
                null,
                null,
                MqttSslProtocols.None
            );

            _client.MqttMsgPublishReceived += async (sender, e) =>
            {
                string messagePayload = Encoding.UTF8.GetString(e.Message);
                string receivedTopic = e.Topic;
                string[] parts = receivedTopic.Split('/');

                if (parts.Length < 2 || string.IsNullOrEmpty(receivedTopic))
                {
                    _logger.LogWarning("Invalid topic or missing data: {topic}", receivedTopic);
                    return;
                }

                string deviceId = parts[0];
                string valueType = parts[^1];

                var integrationEvent = new MqttMessageReceivedEvent(
                    customerId: "unknown",
                    deviceId: deviceId,
                    valueType: valueType,
                    rawPayload: messagePayload,
                    value: messagePayload,
                    source: nameof(MqttService),
                    correlationId: Guid.NewGuid().ToString(),
                    topic: receivedTopic
                );

                await _eventBus.Publish(integrationEvent).ConfigureAwait(false);
                OnMessageReceived?.Invoke(this, integrationEvent);

                if (_settings.isDebug)
                {
                    _logger.LogInformation("[MQTT Service] Device: {deviceId} | Payload: {payload}", deviceId, messagePayload);
                }
            };

            _client.ConnectionClosed += (sender, e) =>
            {
                _logger.LogWarning("Connection to MQTT broker lost.");
            };

            _client.Connect(_settings.clientId, _settings.username, _settings.password);

            if (_client.IsConnected)
            {
                _logger.LogInformation("[MQTT Service]: Connected to broker {broker}", _settings.broker);

                await _eventBus.Publish(new BrokerConnectionEvent(
                    BrokerStatus.Connected,
                    _settings.broker,
                    "Successfully connected to EMQX broker",
                    nameof(MqttService),
                    Guid.NewGuid().ToString()
                ));
            }
        }
        catch (Exception)
        {
            _logger.LogWarning("[{Service}]: Connection failed to {Broker}:{Port}. Diagnosing...",
                nameof(MqttService), _settings.broker, _settings.port);

            var status = BrokerStatus.ConnectionFailed;
            var diagMessage = "MQTT broker refused connection";

            try
            {
                using var pinger = new System.Net.NetworkInformation.Ping();
                var reply = pinger.Send(_settings.broker, 2000);

                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    _logger.LogInformation("[{Service}]: Diagnostic -> Server {Broker} is Online (Ping: {Time}ms), but MQTT broker refused connection.",
                        nameof(MqttService), _settings.broker, reply.RoundtripTime);
                }
                else
                {
                    status = BrokerStatus.ServerUnreachable;
                    diagMessage = $"Server unreachable. Ping status: {reply.Status}";

                    _logger.LogError("[{Service}]: Diagnostic -> Server {Broker} is Offline (Status: {Status}).",
                        nameof(MqttService), _settings.broker, reply.Status);
                }
            }
            catch (Exception pingEx)
            {
                _logger.LogDebug("Ping diagnostic failed: {Message}", pingEx.Message);
            }

            await _eventBus.Publish(new BrokerConnectionEvent(
                status,
                _settings.broker,
                diagMessage,
                nameof(MqttService),
                Guid.NewGuid().ToString()
            ));
        }
    }

    public void Publish(string topic, string message, bool retain = false)
    {
        if (_client == null || !_client.IsConnected)
        {
            _logger.LogWarning("Publish failed: Not connected to broker.");
            return;
        }

        byte[] payload = Encoding.UTF8.GetBytes(message);
        _client.Publish(
            topic,
            payload,
            MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE,
            retain
        );

        if (_settings.isDebug)
        {
            _logger.LogDebug("Published to {topic}: {message}", topic, message);
        }
    }

    public void Subscribe(string topic)
    {
        if (_client == null || !_client.IsConnected)
        {
            _logger.LogWarning("Subscribe failed: Not connected to broker.");
            return;
        }

        _client.Subscribe(
            new[] { topic },
            new[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE }
        );

        _logger.LogInformation("Subscribed to topic: {topic}", topic);
    }

    public bool IsConnected => _client?.IsConnected ?? false;
}
