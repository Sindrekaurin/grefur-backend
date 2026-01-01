using System;
using System.Threading.Tasks;
using grefurBackend.Events.Lifecycle;
using grefurBackend.Infrastructure;
using Microsoft.Extensions.Logging;

namespace grefurBackend.Engines;

public class BootstrapEngine
{
    private readonly EventBus eventBus;
    private readonly ILogger<BootstrapEngine> logger; // Endret fra LoggerEngine til BootstrapEngine

    public BootstrapEngine(EventBus eventBus, ILogger<BootstrapEngine> logger)
    {
        this.eventBus = eventBus;
        this.logger = logger;
    }

    /// <summary>
    /// Starter systemet med minimale lifecycle-events
    /// </summary>
    public async Task start() // PascalFormat for funksjoner
    {
        var correlationId = Guid.NewGuid().ToString();

        // 1. System starter
        await eventBus.Publish(new SystemStartingEvent("BootstrapEngine", correlationId)).ConfigureAwait(false);
        logger.LogInformation("[BootstrapEngine]: SystemStartingEvent published");

        // --- Her kan du senere aktivere andre engines ---

        // 3. System klart
        await eventBus.Publish(new SystemReadyEvent("BootstrapEngine", correlationId)).ConfigureAwait(false);
        logger.LogInformation("[BootstrapEngine]: SystemReadyEvent published");
    }
}