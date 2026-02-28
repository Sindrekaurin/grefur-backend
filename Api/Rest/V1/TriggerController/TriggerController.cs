using Microsoft.AspNetCore.Mvc;
using grefurBackend.Infrastructure;
using System.Text.Json;

namespace grefurBackend.Api.Rest.V1
{
    [ApiController]
    [Route("v1/[controller]")]
    public partial class TriggerController : ControllerBase
    {
        private readonly EventBus _eventBus;
        private readonly ILogger<TriggerController> _logger;

        public TriggerController(EventBus eventBus, ILogger<TriggerController> logger)
        {
            _eventBus = eventBus;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string events, [FromQuery] string? props)
        {
            if (string.IsNullOrWhiteSpace(events)) return BadRequest("No events specified");

            _logger.LogInformation("TriggerController: Received request for events: {Events}", events);

            // Parser JSON-strengen til en Dictionary for enkel tilgang til egenskaper
            var propDictionary = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(props))
            {
                try
                {
                    propDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(props) ?? new();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "TriggerController: Failed to parse props JSON");
                }
            }

            var eventList = events.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var responses = new List<object>();

            foreach (var evt in eventList)
            {
                var trimmedEvent = evt.Trim();
                // Sender ordboken videre til prosessering
                var response = await ProcessTriggerEventAsync(trimmedEvent, propDictionary);
                responses.Add(response);
            }

            return Ok(responses);
        }

        private async Task<object> ProcessTriggerEventAsync(string eventName, Dictionary<string, string> props)
        {
            var correlationId = Guid.NewGuid().ToString();

            _logger.LogInformation("TriggerController: Processing event '{EventName}' with CorrelationId: {CorrelationId}", eventName, correlationId);

            return eventName switch
            {
                "TrainAndPublish" => await HandleTrainAndPublishAsync(correlationId, props),
                //"ChangeCustomerData" => await HandleCustomerChangeAsync(correlationId, props),
                "RetrieveLogs" => await HandleRetrieveLogsAsync(correlationId, props),
                _ => new { Event = eventName, Status = "Unknown event" }
            };
        }
    }
}