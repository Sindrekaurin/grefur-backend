using System;
using System.Globalization;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using grefurBackend.Events.Domain;
using grefurBackend.Events;
using grefurBackend.Infrastructure;
using grefurBackend.Types;
using grefurBackend.Context;
using grefurBackend.Models;
using grefurBackend.Workers;

namespace grefurBackend.Services;

/* Summary of class: High-performance telemetry service for grefur-sensor. 
   Implements 150ms throttling and 1-minute batching with duplicate-safe recovery. */
public class LoggerService : IEventHandler<LogPointEvent>, IDisposable
{
    private static readonly TimeSpan Tier1Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Tier2Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan Tier3Interval = TimeSpan.FromHours(2);

    private static readonly int Tier1DaysThreshold = 7;
    private static readonly int Tier2DaysThreshold = 30;
    private static readonly int Tier3DaysThreshold = 90;
    private static readonly int Tier4DaysThreshold = 180;

    private readonly ILogger<LoggerService> _logger;
    private readonly EventBus _eventBus;
    private readonly IDbContextFactory<TimescaleContext> _timescaleContext;
    private readonly CustomerUsageCoordinator _usageCoordinator;

   

    // --- State Management ---
    private readonly ConcurrentDictionary<string, DateTime> _lastLogTimes = new();
    private ConcurrentBag<SensorReading> _readingBuffer = new();
    private readonly System.Timers.Timer _flushTimer;
    private readonly TimeSpan _minLogInterval = TimeSpan.FromMilliseconds(150);

    private int _successCount = 0;
    private int _errorCount = 0;
    private readonly object _syncLock = new object();
    private DateTime _lastUsedTimestamp = DateTime.MinValue;
    private readonly ConcurrentDictionary<(long Ticks, string Topic, string CustomerId), byte> _currentBatchKeys = new();



    public LoggerService(
        ILogger<LoggerService> logger,
        EventBus eventBus,
        IDbContextFactory<TimescaleContext> timescaleContext,
        CustomerUsageCoordinator usageCoordinator,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _eventBus = eventBus;
        _timescaleContext = timescaleContext;
        _usageCoordinator = usageCoordinator;

        // Register applicaton to flush batch to database while stop signal is given
        appLifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogInformation("[LoggerService] Application stopping. Performing final flush...");
            FlushBatchToDatabaseAsync().GetAwaiter().GetResult();
        });

        // Start batch timer: 1 minute (60000ms)
        _flushTimer = new System.Timers.Timer(60000);
        _flushTimer.Elapsed += async (s, e) => await FlushBatchToDatabaseAsync();
        _flushTimer.AutoReset = true;
        _flushTimer.Enabled = true;

        _eventBus.Subscribe<LogPointEvent>(this);
    }

    /* Summary of function: Validates and throttles telemetry. 
       Uses a thread-safe dictionary to guarantee no duplicates enter the buffer. */
    public async Task<LogPointStatus> logTelemetryAsync(string topic, string payload, string correlationId, string customerId = "default")
    {
        if (string.IsNullOrWhiteSpace(payload) || topic.Contains("//") || (!topic.EndsWith("/value") && !topic.EndsWith("/unit")))
        {
            return LogPointStatus.Received;
        }

        // 1. Throttling per topic (150ms)
        DateTime now = DateTime.UtcNow;
        if (_lastLogTimes.TryGetValue(topic, out var lastTime))
        {
            if (now - lastTime < _minLogInterval) return LogPointStatus.Received;
        }
        _lastLogTimes[topic] = now;

        try
        {
            if (!double.TryParse(payload, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue))
            {
                return LogPointStatus.Received;
            }

            var segments = topic.Split('/');
            if (segments.Length < 2) return LogPointStatus.Received;

            string deviceId = segments[0];
            string property = segments.Last();
            DateTime timestampToUse;

            // 2. Generer unikt tidsstempel og sjekk mot nåværende batch
            lock (_syncLock)
            {
                timestampToUse = DateTime.UtcNow;
                // Hvis vi får inn meldinger ekstremt fort, skyver vi tidsstempelet litt
                if (timestampToUse <= _lastUsedTimestamp)
                {
                    timestampToUse = _lastUsedTimestamp.AddTicks(1);
                }
                _lastUsedTimestamp = timestampToUse;
            }

            // 3. ATOMISK SJEKK: Prøv å legge til nøkkelen i "vaskefatet"
            // Hvis TryAdd feiler, betyr det at nøyaktig denne målingen allerede er i køen
            var key = (timestampToUse.Ticks, deviceId, property);
            if (!_currentBatchKeys.TryAdd(key, 0))
            {
                return LogPointStatus.Received;
            }

            var reading = new SensorReading
            {
                Timestamp = timestampToUse,
                Topic = topic,
                CustomerId = customerId,
                DeviceId = deviceId,
                Property = property,
                Value = numericValue
            };

            _readingBuffer.Add(reading);

            if (customerId != "default")
            {
                _usageCoordinator.BufferCustomerLogPointForUsage(customerId);
            }

            return LogPointStatus.Created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoggerService: Error buffering {Topic}", topic);
            return LogPointStatus.Failed;
        }
    }

    /* Summary of function: Flushes the buffer and clears the key-registry. */
    private async Task FlushBatchToDatabaseAsync()
    {
        if (_readingBuffer.IsEmpty) return;

        var itemsToSave = Interlocked.Exchange(ref _readingBuffer, new ConcurrentBag<SensorReading>()).ToList();
        _currentBatchKeys.Clear();

        if (itemsToSave.Count == 0) return;

        try
        {
            // Sikker batch-størrelse - godt under 10922
            const int batchSize = 4000;
            int totalSaved = 0;

            for (int i = 0; i < itemsToSave.Count; i += batchSize)
            {
                var batch = itemsToSave.Skip(i).Take(batchSize).ToList();

                // Bruk ny context for hver batch
                using var context = await _timescaleContext.CreateDbContextAsync();
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                await context.SensorReadings.AddRangeAsync(batch);
                await context.SaveChangesAsync();

                totalSaved += batch.Count;

                _logger.LogDebug("Saved batch {BatchNumber}: {Count} readings",
                    i / batchSize + 1, batch.Count);
            }

            Interlocked.Add(ref _successCount, totalSaved);
            _logger.LogInformation("Successfully saved all {Count} sensor readings in batches of {BatchSize}",
                itemsToSave.Count, batchSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch save failed with {Count} items, falling back to individual saves",
                itemsToSave.Count);
            await SavePointsIndividuallyAsync(itemsToSave);
        }
    }

    /* Summary of function: Fallback save. Swallows DbUpdateException to keep logs clean. */
    private async Task SavePointsIndividuallyAsync(List<SensorReading> items)
    {
        int localSaved = 0;
        const int batchSize = 2000; // Mindre batcher for fallback

        for (int i = 0; i < items.Count; i += batchSize)
        {
            var batch = items.Skip(i).Take(batchSize).ToList();

            try
            {
                using var context = await _timescaleContext.CreateDbContextAsync();
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                await context.SensorReadings.AddRangeAsync(batch);
                await context.SaveChangesAsync();
                localSaved += batch.Count;
            }
            catch (DbUpdateException)
            {
                // Hvis batch feiler pga duplikater, prøv én og én
                foreach (var item in batch)
                {
                    try
                    {
                        using var context = await _timescaleContext.CreateDbContextAsync();
                        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                        context.SensorReadings.Add(item);
                        await context.SaveChangesAsync();
                        localSaved++;
                    }
                    catch (DbUpdateException)
                    {
                        // Ignorer duplikater - de finnes allerede i databasen
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Individual save error for {Topic}", item.Topic);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch save error for {Count} items", batch.Count);
            }
        }

        Interlocked.Add(ref _successCount, localSaved);
        if (localSaved < items.Count)
        {
            _logger.LogWarning("Saved {Saved}/{Total} readings individually", localSaved, items.Count);
        }
    }

    public async Task Handle(LogPointEvent logPointEvt)
    {
        if (logPointEvt.Status != LogPointStatus.Requested && logPointEvt.Status != LogPointStatus.Received) return;
        await logTelemetryAsync(logPointEvt.Topic, logPointEvt.Value, logPointEvt.CorrelationId, logPointEvt.CustomerId);
    }

    // --- Maintenance & Queries ---

    public async Task<IReadOnlyList<LogPoint>> GetLogAsync(string topic, DateTime start, DateTime end, CancellationToken ct = default)
    {
        using var context = await _timescaleContext.CreateDbContextAsync(ct);
        var seg = topic.Split('/');
        return await context.SensorReadings.AsNoTracking()
            .Where(r => r.DeviceId == seg[0] && r.Property == seg.Last() && r.Timestamp >= start && r.Timestamp <= end)
            .OrderBy(r => r.Timestamp).Select(r => new LogPoint(r.Timestamp, r.Value)).ToListAsync(ct);
    }

    /* Summary of function: Retrieves the last N readings for a specific device. 
       Uses AsNoTracking for high performance. */
    public async Task<List<SensorReading>> getLatestLogsAsync(string deviceId, int limit = 10)
    {
        using var context = await _timescaleContext.CreateDbContextAsync();
        return await context.SensorReadings
            .AsNoTracking()
            .Where(r => r.DeviceId == deviceId)
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .ToListAsync();
    }


    public async Task RunMaintenanceSmoothingAsync(CancellationToken ct = default)
    {
        using var context = await _timescaleContext.CreateDbContextAsync(ct);
        DateTime now = DateTime.UtcNow;
        await DownsampleRangeAsync(context, now.AddDays(-30), now.AddDays(-7), Tier1Interval, ct);
        await DownsampleRangeAsync(context, now.AddDays(-90), now.AddDays(-30), Tier2Interval, ct);
        await DownsampleRangeAsync(context, now.AddDays(-180), now.AddDays(-90), Tier3Interval, ct);
    }

    private async Task DownsampleRangeAsync(TimescaleContext context, DateTime start, DateTime end, TimeSpan interval, CancellationToken ct)
    {
        var streams = await context.SensorReadings.AsNoTracking()
            .Where(r => r.Timestamp >= start && r.Timestamp < end)
            .Select(r => new { r.Topic, r.DeviceId, r.Property, r.CustomerId })
            .Distinct().ToListAsync(ct);

        foreach (var s in streams)
        {
            var raw = await context.SensorReadings
                .Where(r => r.Topic == s.Topic && r.Timestamp >= start && r.Timestamp < end)
                .OrderBy(r => r.Timestamp).ToListAsync(ct);

            if (raw.Count <= 1) continue;

            var smoothed = raw.GroupBy(r => (r.Timestamp.Ticks / interval.Ticks) * interval.Ticks)
                .Select(g => new SensorReading
                {
                    Timestamp = new DateTime(g.Key).AddTicks(interval.Ticks / 2),
                    Value = g.Average(x => x.Value),
                    Topic = s.Topic,
                    DeviceId = s.DeviceId,
                    Property = s.Property,
                    CustomerId = s.CustomerId
                }).ToList();

            using var tx = await context.Database.BeginTransactionAsync(ct);
            try
            {
                context.SensorReadings.RemoveRange(raw);
                await context.SaveChangesAsync(ct);
                context.SensorReadings.AddRange(smoothed);
                await context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch { await tx.RollbackAsync(ct); }
        }
    }

    public async Task<int> UpdateTopicNameAsync(string customerId, string oldTopic, string newTopic, CancellationToken ct = default)
    {
        using var context = await _timescaleContext.CreateDbContextAsync(ct);
        var seg = newTopic.Split('/');
        return await context.SensorReadings
            .Where(r => r.CustomerId == customerId && r.Topic == oldTopic)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Topic, newTopic).SetProperty(r => r.DeviceId, seg[0]).SetProperty(r => r.Property, seg.Last()), ct);
    }

    public (int success, int error) getStatistics() => (_successCount, _errorCount);
    public void resetStatistics() { Interlocked.Exchange(ref _successCount, 0); Interlocked.Exchange(ref _errorCount, 0); }

    /* Summary of function: Standard cleanup of resources. 
   Final data flush is now handled by ApplicationStopping hook. */
    public void Dispose()
    {
        _flushTimer?.Stop();
        _flushTimer?.Dispose();
    }
}