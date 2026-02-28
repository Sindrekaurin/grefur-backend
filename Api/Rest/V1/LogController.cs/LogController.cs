using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using grefurBackend.Context;
using grefurBackend.Models;
using Microsoft.AspNetCore.Authorization;

namespace grefurBackend.Api.Rest.V1.Logs;

[ApiController]
[Route("api/rest/v1/logs")]
[Authorize]
public class LogController : ControllerBase
{
    private readonly IDbContextFactory<TimescaleContext> _timescaleContext;
    private readonly ILogger<LogController> _logger;

    public LogController(
        IDbContextFactory<TimescaleContext> timescaleContext,
        ILogger<LogController> logger)
    {
        _timescaleContext = timescaleContext;
        _logger = logger;
    }

    [HttpGet("{*topic}")]
    public async Task<IActionResult> GetLogs(
        string topic,
        [FromQuery] int? limit,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        _logger.LogInformation("Grefur-Sensor: Fetching logs for topic {Topic} (Limit: {Limit}, From: {From}, To: {To})",
            topic, limit, from, to);

        var decodedTopic = global::System.Net.WebUtility.UrlDecode(topic);
        _logger.LogInformation("Grefur-Sensor: Fetching logs for topic {Topic} (Decoded: {DecodedTopic})",
        topic, decodedTopic);

        using var context = await _timescaleContext.CreateDbContextAsync();

        var query = context.SensorReadings
            .Where(l => l.Topic == decodedTopic)
            .OrderByDescending(l => l.Timestamp)
            .AsQueryable();

        if (from.HasValue) query = query.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(l => l.Timestamp <= to.Value);
        if (limit.HasValue) query = query.Take(limit.Value);

        var data = await query.ToListAsync();

        if (data.Count > 0)
        {
            _logger.LogInformation("Grefur-Sensor: Found {Count} data points for topic {Topic}. Latest reading: {Timestamp}",
                data.Count, topic, data[0].Timestamp);
        }
        else
        {
            _logger.LogWarning("Grefur-Sensor: No data points found for topic {Topic} with current filters.", topic);
        }

        return Ok(new { success = true, count = data.Count, data });
    }

    [HttpGet("{topic}/derivative")]
    public async Task<IActionResult> GetLogDerivative(
        string topic,
        [FromQuery] int limit = 100,
        [FromQuery] DateTime? from = null)
    {
        using var context = await _timescaleContext.CreateDbContextAsync();

        // {Henter rådata for å beregne derivert}
        var query = context.SensorReadings
            .Where(l => l.Topic == topic)
            .OrderByDescending(l => l.Timestamp);

        var readings = await (from != null
            ? query.Where(l => l.Timestamp >= from).ToListAsync()
            : query.Take(limit + 1).ToListAsync());

        if (readings.Count < 2) return Ok(new { success = true, data = new List<object>() });

        var derivatives = new List<object>();

        // {Beregner endring per sekund mellom hvert målepunkt}
        for (int i = 0; i < readings.Count - 1; i++)
        {
            var current = readings[i];
            var next = readings[i + 1];

            var timeDiffSeconds = (current.Timestamp - next.Timestamp).TotalSeconds;

            if (timeDiffSeconds > 0)
            {
                var derivativeValue = (current.Value - next.Value) / timeDiffSeconds;
                derivatives.Add(new
                {
                    timestamp = current.Timestamp,
                    derivative = derivativeValue,
                    unit = $"{current.Property}/s"
                });
            }
        }

        return Ok(new { success = true, topic, data = derivatives });
    }
}