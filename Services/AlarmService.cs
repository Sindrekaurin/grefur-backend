using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using grefurBackend.Context; // Ensure this points to where your MySQL context is
using grefurBackend.Models.AlarmConfiguration;

namespace grefurBackend.Services;

/* Summary of class: Service for anomaly detection using TimescaleDB for history and MySQL for configurations. */
public class AlarmService
{
    private readonly ILogger<AlarmService> _logger;
    private readonly IDbContextFactory<MySqlContext> _mySqlContextFactory; // Changed to MySql context factory
    private readonly Dictionary<string, List<double>> _telemetryHistory = new();
    private const int HistoryLimit = 15;

    public AlarmService(
        ILogger<AlarmService> logger,
        IDbContextFactory<MySqlContext> mySqlContextFactory)
    {
        _logger = logger;
        _mySqlContextFactory = mySqlContextFactory;
    }

    /* Summary of function: Analyzes value against in-memory history. */
    public bool AnalyzeValue(string topic, double value, out string message)
    {
        message = string.Empty;
        if (!_telemetryHistory.ContainsKey(topic)) _telemetryHistory[topic] = new List<double>();

        var history = _telemetryHistory[topic];
        history.Add(value);
        if (history.Count > HistoryLimit) history.RemoveAt(0);

        if (history.Count < 5) return false;

        double average = history.Average();
        double threshold = average * 0.4;
        double deviation = Math.Abs(value - average);

        if (deviation > threshold)
        {
            message = $"Grefur-Alarm: Anomaly detected on {topic}. Value: {value}, Avg: {average:F2}";
            _logger.LogWarning(message);
            return true;
        }

        return false;
    }

    /* Summary of function: Retrieves configurations from the MySQL database factory. */
    public async Task<List<MlAlarmConfiguration>> GetAllMlAlarmConfigurationsAsync(CancellationToken ct = default)
    {
        using var context = await _mySqlContextFactory.CreateDbContextAsync(ct);
        return await context.MlAlarmConfigurations
            .AsNoTracking()
            .ToListAsync(ct);
    }
}