/*
 * LoggerService.cs 
 */

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using grefurBackend.Events.Domain;
using grefurBackend.Events;
using grefurBackend.Infrastructure;
using grefurBackend.Types;
using System.Globalization;
using System.Collections.Concurrent;

namespace grefurBackend.Services;

public class LoggerService : IEventHandler<LogPointEvent>
{
    private readonly ILogger<LoggerService> _logger;
    private readonly EventBus _eventBus;

    private readonly string _databasePath;
    private int _successCount = 0;
    private int _errorCount = 0;

    private string _lastCorrelationId = string.Empty;
    private readonly object _syncLock = new object();

    public LoggerService(ILogger<LoggerService> logger, EventBus eventBus)
    {
        _logger = logger;

        // Finn rotmappen til prosjektet mer robust
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // Sjekk om vi kjører i utviklingsmodus (bin/Debug...) eller publisert modus
        if (baseDir.Contains(Path.Combine("bin", "Debug")) || baseDir.Contains(Path.Combine("bin", "Release")))
        {
            // Gå opp 4 nivåer hvis vi er i bin/Debug/net9.0/
            _databasePath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "DATABASE"));
        }
        else
        {
            // Hvis publisert, anta at DATABASE ligger i samme mappe som .dll
            _databasePath = Path.Combine(baseDir, "DATABASE");
        }

        if (!Directory.Exists(_databasePath))
        {
            Directory.CreateDirectory(_databasePath);
            _logger.LogInformation("LoggerService: Created database directory at {path}", _databasePath);
        }

        _eventBus = eventBus;

        // Subscribe to log point event
        _eventBus.Subscribe<LogPointEvent>(this);
    }

    // Public API used by LoggingEngine
    // Add this to your class fields to track state
    private readonly ConcurrentDictionary<string, (string Timestamp, string Payload)> _lastLogEntries = new();

    public async Task<LogPointStatus> logTelemetryAsync(string topic, string payload, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(payload) || (!topic.EndsWith("/value") && !topic.EndsWith("/unit")))
        {
            return LogPointStatus.Received;
        }

        string currentTimestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        lock (_syncLock)
        {
            // 1. Standard MQTT Correlation Check
            if (_lastCorrelationId == correlationId) return LogPointStatus.Received;
            _lastCorrelationId = correlationId;

            // 2. Time-Based Throttle Guard
            // If the same topic has the same payload within the same second, ignore it.
            if (_lastLogEntries.TryGetValue(topic, out var lastEntry))
            {
                if (lastEntry.Timestamp == currentTimestamp && lastEntry.Payload == payload)
                {
                    return LogPointStatus.Received; // Exact duplicate within the same second
                }
            }

            // Update the cache with the current entry
            _lastLogEntries[topic] = (currentTimestamp, payload);
        }

        try
        {
            string safeFileName = topic.Replace("/", "_").Replace("\\", "_");
            string logLine = $"{currentTimestamp};{payload}{Environment.NewLine}";

            string logFilePath = Path.Combine(_databasePath, $"{safeFileName}.log");
            string csvFilePath = Path.Combine(_databasePath, $"{safeFileName}.csv");

            // Concurrent write to both log and csv
            await Task.WhenAll(
                File.AppendAllTextAsync(logFilePath, logLine),
                File.AppendAllTextAsync(csvFilePath, logLine)
            ).ConfigureAwait(false);

            Interlocked.Increment(ref _successCount);
            return LogPointStatus.Created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoggerService: Failed to log topic {topic}", topic);
            return LogPointStatus.Failed;
        }
    }

    // Keep existing event handler but delegate to logTelemetryAsync
    public async Task Handle(LogPointEvent logPointEvt)
    {
        // 1. Quick exit: Only process if status is Requested/Received and value exists
        if (logPointEvt.Status != LogPointStatus.Requested && logPointEvt.Status != LogPointStatus.Received)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(logPointEvt.Value))
        {
            return;
        }

        // 2. Structured logging (Using PascalCase CorrelationId)
        _logger.LogDebug("Processing telemetry logging for Topic: {Topic}, CorrelationId: {CorrelationId}",
            logPointEvt.Topic, logPointEvt.CorrelationId);

        // 3. Execute logging with the de-bouncer logic
        LogPointStatus resultStatus = await logTelemetryAsync(
            logPointEvt.Topic,
            logPointEvt.Value,
            logPointEvt.CorrelationId
        ).ConfigureAwait(false);

        // 4. Trace result
        if (resultStatus == LogPointStatus.Created)
        {
            _logger.LogInformation("Telemetry successfully logged: {Topic} (ID: {CorrelationId})",
                logPointEvt.Topic, logPointEvt.CorrelationId);
        }
    }

    public async Task<IReadOnlyList<LogPoint>> getLogAsync(
        string topic,
        DateTime startDateTime,
        DateTime endDateTime,
        CancellationToken cancellationToken = default)
    {
        var result = new List<LogPoint>();

        // TODO: TimescaleDB
        // Denne metoden er per i dag filbasert.
        // Ved overgang til TimescaleDB skal hele blokken fra filoppslag til parsing
        // erstattes av én SQL-spørring:
        //
        // SELECT time, value
        // FROM measurements
        // WHERE measurement_id = @topic
        //   AND time BETWEEN @startDateTime AND @endDateTime
        // ORDER BY time ASC;

        string safeFileName = topic.Replace("/", "_").Replace("\\", "_");
        string csvFilePath = Path.Combine(_databasePath, $"{safeFileName}.csv");

        // Debug logging for comfirming path of truth
        _logger.LogInformation("Searching for log file at: {Path}", csvFilePath);

        if (!File.Exists(csvFilePath))
        {
            _logger.LogWarning("Log file not found for topic: {Topic}", topic);
            return result;
        } else
        {
            _logger.LogInformation("Log file found for topic: {Topic}", topic);
        }

            try
            {
                using var stream = new FileStream(
                csvFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

                using var reader = new StreamReader(stream, Encoding.UTF8);

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // TODO: TimescaleDB
                    // Parsing av CSV-linje tilsvarer mapping av SQL-resultat (time, value)
                    // Denne delen forsvinner helt når data hentes direkte fra database.

                    var parts = line.Split(';', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 2)
                        continue;

                    if (!DateTime.TryParse(parts[0], out var timestamp))
                        continue;

                    if (timestamp < startDateTime || timestamp > endDateTime)
                        continue;

                    if (!double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                        continue;

                result.Add(new LogPoint(timestamp, value));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoggerService: Error reading log file {path}", csvFilePath);
            }
        return result;
    }

    public async Task<bool> saveToFile(string fileName, string payload)
    {
        try
        {
            // Sørg for at vi har en ren sti og kombiner med database-mappen
            string filePath = Path.Combine(_databasePath, fileName);

            // Sjekk om mappen eksisterer (sikkerhetskopi)
            if (!Directory.Exists(_databasePath))
            {
                Directory.CreateDirectory(_databasePath);
            }

            await File.AppendAllTextAsync(filePath, payload).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoggerService: Failed to save to file {fileName}", fileName);
            return false;
        }
    }

    public async Task<bool> saveBinaryFileAsync(string fileName, byte[] data)
    {
        try
        {
            string filePath = Path.Combine(_databasePath, fileName);
            await File.WriteAllBytesAsync(filePath, data).ConfigureAwait(false);

            _logger.LogInformation("LoggerService: Binary file saved to {Path}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoggerService: Failed to save binary file {FileName}", fileName);
            return false;
        }
    }

    public (int success, int error) getStatistics()
    {
        return (_successCount, _errorCount);
    }

    public void resetStatistics()
    {
        Interlocked.Exchange(ref _successCount, 0);
        Interlocked.Exchange(ref _errorCount, 0);
    }
}