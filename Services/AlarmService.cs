using Microsoft.Extensions.Logging;

namespace grefurBackend.Services;

public class AlarmService
{
    private readonly ILogger<AlarmService> _logger;
    // keep eventBus parameter if DI still passes it; not used here but preserved to avoid changing registrations
    private readonly object? _unusedEventBus;
    private readonly Dictionary<string, List<double>> _telemetryHistory = new();
    private const int HistoryLimit = 15;

    public AlarmService(ILogger<AlarmService> logger, object? eventBus = null)
    {
        _logger = logger;
        _unusedEventBus = eventBus;
    }

    /// <summary>
    /// Analyze an incoming value for the given topic.
    /// Returns true if an alarm should be raised and sets a descriptive message.
    /// </summary>
    public bool AnalyzeValue(string topic, double value, out string message)
    {
        message = string.Empty;

        if (!_telemetryHistory.ContainsKey(topic))
        {
            _telemetryHistory[topic] = new List<double>();
        }

        var history = _telemetryHistory[topic];
        history.Add(value);

        if (history.Count > HistoryLimit) history.RemoveAt(0);

        if (history.Count < 5) return false; // Need some data before deciding

        double average = history.Average();
        double threshold = average * 0.4; // 40% deviation considered anomalous
        double deviation = Math.Abs(value - average);

        if (deviation > threshold)
        {
            message = $"Grefur-Alarm: Anomaly detected on {topic}. Value: {value}, Avg: {average:F2}";
            _logger.LogWarning(message);
            return true;
        }

        return false;
    }
}