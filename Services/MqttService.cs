using System;
using System.Text;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Microsoft.Extensions.Logging;
using grefurBackend.Events;
using grefurBackend.Events.Integration;
using grefurBackend.Infrastructure;
using System.Net.NetworkInformation;
using grefurBackend.Types;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace grefurBackend.Services;

public class MqttSettings
{
    public string username { get; set; } = "DATALAKE";
    public string password { get; set; } = "DATALAKE2024";
    public string broker { get; set; } = "192.168.10.183";
    public int port { get; set; } = 1883;
    public int apiPort { get; set; } = 18083;
    public string clientId { get; set; } = "grefurBackendServer";
    public bool isDebug { get; set; } = true;
}

public class MqttService
{
    private MqttClient? _client;
    private readonly ILogger<MqttService> _logger;
    private readonly MqttSettings _settings;
    private readonly EventBus _eventBus;
    private readonly string _apiKey;
    private readonly string _apiSecret;

    public event EventHandler<MqttMessageReceivedEvent>? OnMessageReceived;

    public MqttService(
        ILogger<MqttService> logger,
        MqttSettings settings,
        EventBus eventBus)
    {
        _logger = logger;
        _settings = settings;
        _eventBus = eventBus;
        _apiKey = DotNetEnv.Env.GetString("BROKER_API_KEY") ?? string.Empty;
        _apiSecret = DotNetEnv.Env.GetString("BROKER_SECRET_KEY") ?? string.Empty;
    }

    public async Task<bool> CreateBrokerUserAsync(string username, string password)
    {
        try
        {
            using var httpClient = new HttpClient();
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_apiKey}:{_apiSecret}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var payload = new
            {
                user_id = username,
                password = password,
            };

            var url = $"http://{_settings.broker}:{_settings.apiPort}/api/v5/authentication/password_based:built_in_database/users";
            var response = await httpClient.PostAsJsonAsync(url, payload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[MQTT Service]: Created broker user {Username}", username);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("[MQTT Service]: API Error {Status}: {Error}", response.StatusCode, errorContent);
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[MQTT Service]: Exception in CreateBrokerUserAsync");
            return false;
        }
    }

    public async Task<bool> DeleteBrokerUserAsync(string username)
    {
        try
        {
            using var httpClient = new HttpClient();
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_apiKey}:{_apiSecret}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var url = $"http://{_settings.broker}:{_settings.apiPort}/api/v5/authentication/password_based:built_in_database/users/{username}";
            var response = await httpClient.DeleteAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[MQTT Service]: Exception in DeleteBrokerUserAsync for {Username}", username);
            return false;
        }
    }

    public async Task Connect()
    {
        var connectionCorrelationId = Guid.NewGuid().ToString();

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

            _client.MqttMsgPublishReceived += async (sender, eventArgs) =>
            {
                string payloadString = Encoding.UTF8.GetString(eventArgs.Message);
                string topicString = eventArgs.Topic;
                string[] topicParts = topicString.Split('/');

                if (topicParts.Length < 2 || string.IsNullOrEmpty(topicString))
                {
                    _logger.LogWarning("[MQTT Service]: Invalid topic structure {Topic}", topicString);
                    return;
                }

                string deviceIdString = topicParts[0];
                string valueTypeString = topicParts[^1];
                string messageCorrelationId = Guid.NewGuid().ToString();

                var messageReceivedEvent = new MqttMessageReceivedEvent(
                    customerId: "unknown",
                    deviceId: deviceIdString,
                    valueType: valueTypeString,
                    rawPayload: payloadString,
                    value: payloadString,
                    source: nameof(MqttService),
                    correlationId: messageCorrelationId,
                    topic: topicString
                );

                await _eventBus.Publish(messageReceivedEvent).ConfigureAwait(false);
                OnMessageReceived?.Invoke(this, messageReceivedEvent);

                if (_settings.isDebug)
                {
                    _logger.LogDebug("[MQTT Service]: Published internal event for {Topic}", topicString);
                }
            };

            _client.ConnectionClosed += (sender, eventArgs) =>
            {
                _logger.LogWarning("[MQTT Service]: Connection lost to {Broker}", _settings.broker);

                var disconnectEvent = new BrokerConnectionEvent(
                    BrokerStatus.Disconnected,
                    _settings.broker,
                    "Lost connection to broker",
                    nameof(MqttService),
                    Guid.NewGuid().ToString()
                );

                _eventBus.Publish(disconnectEvent).Wait();
            };

            _client.Connect(_settings.clientId, _settings.username, _settings.password);

            if (_client.IsConnected)
            {
                _logger.LogInformation("[MQTT Service]: Connected to {Broker} on port {Port}", _settings.broker, _settings.port);

                var connectedEvent = new BrokerConnectionEvent(
                    BrokerStatus.Connected,
                    _settings.broker,
                    "Connected successfully",
                    nameof(MqttService),
                    Guid.NewGuid().ToString()
                );

                await _eventBus.Publish(connectedEvent);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[MQTT Service]: Connection failure to {Broker}", _settings.broker);
            await RunDiagnostics(connectionCorrelationId);
        }
    }

    /* Summary of function: Returns the public connection settings for the MQTT broker */
    public MqttSettings GetBrokerSettings()
    {
        return _settings;
    }

    private async Task RunDiagnostics(string correlationId)
    {
        var status = BrokerStatus.ConnectionFailed;
        var message = "Broker refused connection";

        try
        {
            using var pinger = new Ping();
            var reply = pinger.Send(_settings.broker, 2000);

            if (reply.Status == IPStatus.Success)
            {
                _logger.LogInformation("[MQTT Service]: Diagnostic - {Broker} is reachable via Ping", _settings.broker);
                message = "Server is online but MQTT service is unreachable";
            }
            else
            {
                status = BrokerStatus.ServerUnreachable;
                message = $"Server unreachable. Ping status: {reply.Status}";
                _logger.LogError("[MQTT Service]: Diagnostic - {Broker} is offline", _settings.broker);
            }
        }
        catch (Exception diagnosticException)
        {
            _logger.LogDebug("[MQTT Service]: Diagnostic ping failed: {Error}", diagnosticException.Message);
        }

        await _eventBus.Publish(new BrokerConnectionEvent(
            status,
            _settings.broker,
            message,
            nameof(MqttService),
            correlationId
        ));
    }

    public void Publish(string topic, string message, bool retain = false)
    {
        if (_client == null || !_client.IsConnected)
        {
            _logger.LogWarning("[MQTT Service]: Publish failed - not connected");
            return;
        }

        byte[] payloadBytes = Encoding.UTF8.GetBytes(message);
        _client.Publish(topic, payloadBytes, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, retain);
    }

    public void Subscribe(string topic)
    {
        if (_client == null || !_client.IsConnected) return;
        _client.Subscribe(new[] { topic }, new[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        _logger.LogInformation("[MQTT Service]: Subscribed to {Topic}", topic);
    }

    public void Unsubscribe(string topic)
    {
        if (_client == null || !_client.IsConnected) return;
        _client.Unsubscribe(new[] { topic });
        _logger.LogInformation("[MQTT Service]: Unsubscribed from {Topic}", topic);
    }

    public bool IsConnected => _client?.IsConnected ?? false;
}