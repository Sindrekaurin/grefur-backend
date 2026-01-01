using System;
using Microsoft.Extensions.DependencyInjection;
using grefurBackend.Services;
using grefurBackend.Engines;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using grefurBackend.Infrastructure;

namespace grefurBackend;

public class Worker : BackgroundService
{
    private readonly MqttService _mqttService;
    private readonly BootstrapEngine _bootstrapEngine;
    private readonly IServiceProvider _services;
    private readonly ILogger<Worker> _logger;
    private bool _engineStarted = false;
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private int _healthCheckCounter = 0;

    public Worker(
        MqttService mqttService,
        BootstrapEngine bootstrapEngine,
        IServiceProvider services,
        ILogger<Worker> logger)
    {
        _mqttService = mqttService;
        _bootstrapEngine = bootstrapEngine;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Worker]: Starting Grefur-backend...");

        try
        {
            // Initial resolve of engines to ensure they are ready for events
            ResolveEngines();

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_mqttService.IsConnected)
                {
                    _logger.LogWarning("[Worker]: MQTT disconnected, attempting to connect...");

                    try
                    {
                        await _mqttService.Connect().ConfigureAwait(false);

                        if (_mqttService.IsConnected)
                        {
                            _logger.LogInformation("[Worker]: Connection established. Re-initializing bootstrap...");

                            // Re-run bootstrap to setup subscriptions and topology on the broker
                            await _bootstrapEngine.start().ConfigureAwait(false);
                            _engineStarted = true;

                            _logger.LogInformation("[Worker]: Bootstrap re-initialized successfully.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[Worker]: Failed to connect or bootstrap.");
                        _engineStarted = false;
                    }
                }

                // Perform health check and update internal metrics
                await PerformHealthCheck(stoppingToken);

                // Wait 5 seconds before next connection check/health check
                await Task.Delay(5000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[Worker]: Service is shutting down gracefully.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[Worker]: Fatal error in background worker loop.");
        }
    }

    // Force DI to construct all engines so they subscribe to lifecycle events before bootstrap runs.
    // Add or remove services here to match engines registered in Program.cs.
    private void ResolveEngines()
    {
        try
        {
            _services.GetRequiredService<CustomerLoadEngine>();
            _services.GetRequiredService<CacheWarmupEngine>();
            _services.GetRequiredService<DeviceDiscoveryEngine>();
            _services.GetRequiredService<TopicTopologyEngine>();
            _services.GetRequiredService<ValueHandlerEngine>();
            _services.GetRequiredService<SubscriptionEngine>();
            _services.GetRequiredService<AlarmEngine>();
            _services.GetRequiredService<EventLoggerService>();
            _services.GetRequiredService<LoggerEngine>();
            _services.GetRequiredService<PredictionEngine>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Worker]: One or more engines failed to resolve.");
        }
    }

    private async Task PerformHealthCheck(CancellationToken ct)
    {
        // Run health check every 30 seconds
        if ((DateTime.UtcNow - _lastHealthCheck).TotalSeconds >= 30)
        {
            _healthCheckCounter++;

            try
            {
                _logger.LogInformation(
                    "[Worker]: Health check #{HealthCheckCount} - System status: {Status}",
                    _healthCheckCounter,
                    GetSystemStatus()
                );

                if (!_mqttService.IsConnected)
                {
                    _logger.LogWarning("[Worker]: Health check failed - MQTT disconnected");
                    _engineStarted = false;
                }
                else
                {
                    _logger.LogDebug("[Worker]: Health check passed - All systems operational");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Worker]: Health check failed with exception");
                _engineStarted = false;
            }
            finally
            {
                _lastHealthCheck = DateTime.UtcNow;
            }
        }

        await Task.CompletedTask;
    }

    private string GetSystemStatus()
    {
        return _engineStarted ? "Engines started" : "Engines not started";
    }
}