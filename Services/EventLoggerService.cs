using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using grefurBackend.Infrastructure;
using grefurBackend.Events;

namespace grefurBackend.Services;

public class EventLoggerService : IEventHandler<Event>, IDisposable
{
    private readonly ILogger<EventLoggerService> logger;
    private readonly string logFilePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);
    private readonly int maxLogLines = 1000;

    // Kø for lynrask lagring i minnet
    private readonly ConcurrentQueue<string> logQueue = new();
    private readonly System.Timers.Timer flushTimer;

    private string lastCorrelationId = string.Empty;
    private readonly object syncLock = new();

    private int successCount = 0;
    private int errorCount = 0;

    public EventLoggerService(EventBus bus, ILogger<EventLoggerService> logger)
    {
        this.logger = logger;
        this.logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "EventBus.log");

        if (!File.Exists(this.logFilePath))
        {
            File.Create(this.logFilePath).Dispose();
        }

        // Sett opp en timer som flusher køen til disk hvert sekund
        flushTimer = new System.Timers.Timer(1000);
        flushTimer.Elapsed += async (s, e) => await FlushLogToFile();
        flushTimer.AutoReset = true;
        flushTimer.Enabled = true;

        bus.Subscribe<Event>(this);
    }

    public Task Handle(Event evt)
    {
        lock (syncLock)
        {
            if (!string.IsNullOrEmpty(evt.CorrelationId) && lastCorrelationId == evt.CorrelationId)
            {
                return Task.CompletedTask;
            }
            lastCorrelationId = evt.CorrelationId ?? string.Empty;
        }

        // I stedet for disk-I/O, legger vi bare strengen i køen (veldig lav CPU-last)
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logLine = $"{timestamp};{evt.GetType().Name};{evt.CorrelationId};{evt.EventId}";

        logQueue.Enqueue(logLine);
        Interlocked.Increment(ref successCount);

        return Task.CompletedTask;
    }

    private async Task FlushLogToFile()
    {
        if (logQueue.IsEmpty) return;

        await fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Tøm køen over i en liste
            var batch = new List<string>();
            while (logQueue.TryDequeue(out var line))
            {
                batch.Add(line);
            }

            if (batch.Count > 0)
            {
                // Skriv alle linjene i én operasjon
                await File.AppendAllLinesAsync(logFilePath, batch).ConfigureAwait(false);

                // Trim fila etter hver store flush
                await TrimLogFile().ConfigureAwait(false);

                // Print status til konsoll
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[EventLoggerService] Batched {batch.Count} events to disk. Total: {successCount}");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("[EventLoggerService]: Flush failed: {Message}", ex.Message);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task TrimLogFile()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(logFilePath).ConfigureAwait(false);
            if (lines.Length > maxLogLines)
            {
                var trimmedLines = lines.Skip(lines.Length - maxLogLines);
                await File.WriteAllLinesAsync(logFilePath, trimmedLines).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("[EventLoggerService]: Trim failed: {Message}", ex.Message);
        }
    }

    public (int success, int error) GetStatistics() => (successCount, errorCount);

    public void ResetStatistics()
    {
        Interlocked.Exchange(ref successCount, 0);
        Interlocked.Exchange(ref errorCount, 0);
    }

    public void Dispose()
    {
        flushTimer?.Stop();
        flushTimer?.Dispose();
        fileLock?.Dispose();
    }
}