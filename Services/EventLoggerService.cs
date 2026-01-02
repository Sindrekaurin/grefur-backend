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
    private readonly string eventLogPath;
    private readonly string errorLogPath;
    private readonly SemaphoreSlim fileLock = new(1, 1);
    private readonly int maxLogLines = 1000;

    private readonly ConcurrentQueue<string> eventQueue = new();
    private readonly ConcurrentQueue<string> errorQueue = new();
    private readonly System.Timers.Timer flushTimer;

    private string lastCorrelationId = string.Empty;
    private readonly object syncLock = new();

    private int successCount = 0;
    private int errorCount = 0;

    public EventLoggerService(EventBus bus, ILogger<EventLoggerService> logger)
    {
        this.logger = logger;
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // Definerer begge filstiene
        this.eventLogPath = Path.Combine(baseDir, "..", "..", "..", "EventBus.log");
        this.errorLogPath = Path.Combine(baseDir, "..", "..", "..", "ErrorLog.log");

        EnsureFileExists(this.eventLogPath);
        EnsureFileExists(this.errorLogPath);

        flushTimer = new System.Timers.Timer(1000);
        flushTimer.Elapsed += async (s, e) => await FlushLogs();
        flushTimer.AutoReset = true;
        flushTimer.Enabled = true;

        bus.Subscribe<Event>(this);
    }

    private void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
        {
            File.Create(path).Dispose();
        }
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

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        if (evt is ErrorEvent errorEvt)
        {
            Interlocked.Increment(ref errorCount);
            // Formaterer for ErrorLog.log
            string errorLine = $"{timestamp};{errorEvt.Level};{errorEvt.ErrorCode};{errorEvt.CorrelationId};{errorEvt.Message};{errorEvt.ExceptionDetails}";
            errorQueue.Enqueue(errorLine);
        }
        else
        {
            Interlocked.Increment(ref successCount);
            // Formaterer for EventBus.log
            string logLine = $"{timestamp};{evt.GetType().Name};{evt.CorrelationId};{evt.EventId}";
            eventQueue.Enqueue(logLine);
        }

        return Task.CompletedTask;
    }

    private async Task FlushLogs()
    {
        if (eventQueue.IsEmpty && errorQueue.IsEmpty) return;

        await fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // 1. Vanlige events rutes til EventBus.log
            if (!eventQueue.IsEmpty)
            {
                await ProcessQueue(eventQueue, eventLogPath, ConsoleColor.Cyan, "Events");
            }

            // 2. Feilmeldinger rutes til ErrorLog.log (Rettet her)
            if (!errorQueue.IsEmpty)
            {
                await ProcessQueue(errorQueue, errorLogPath, ConsoleColor.Red, "ERRORS");
            }
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task ProcessQueue(ConcurrentQueue<string> queue, string path, ConsoleColor color, string label)
    {
        var batch = new List<string>();
        while (queue.TryDequeue(out var line))
        {
            batch.Add(line);
        }

        if (batch.Count > 0)
        {
            await File.AppendAllLinesAsync(path, batch).ConfigureAwait(false);
            await TrimFile(path).ConfigureAwait(false);

            Console.ForegroundColor = color;
            Console.WriteLine($"[EventLoggerService] Batched {batch.Count} {label} to {Path.GetFileName(path)}.");
            Console.ResetColor();
        }
    }

    private async Task TrimFile(string path)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(false);
            if (lines.Length > maxLogLines)
            {
                var trimmedLines = lines.Skip(lines.Length - maxLogLines);
                await File.WriteAllLinesAsync(path, trimmedLines).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("[EventLoggerService]: Trim failed for {Path}: {Message}", path, ex.Message);
        }
    }

    public (int success, int error) GetStatistics() => (successCount, errorCount);

    public void Dispose()
    {
        flushTimer?.Stop();
        flushTimer?.Dispose();
        fileLock?.Dispose();
    }
}