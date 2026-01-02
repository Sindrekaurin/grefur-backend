using Microsoft.AspNetCore.Mvc;
using grefurBackend.Infrastructure;
using grefurBackend.Services;

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
        public async Task<IActionResult> Get([FromQuery] string events)
        {
            if (string.IsNullOrWhiteSpace(events)) return BadRequest("No events specified");

            _logger.LogInformation("TriggerController: Received request for events: {Events}", events);

            var eventList = events.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var responses = new List<object>();

            foreach (var evt in eventList)
            {
                var trimmedEvent = evt.Trim();
                var response = await ProcessTriggerEventAsync(trimmedEvent);
                responses.Add(response);
            }

            return Ok(responses);
        }

        private async Task<object> ProcessTriggerEventAsync(string eventName)
        {
            var correlationId = Guid.NewGuid().ToString();

            _logger.LogInformation("TriggerController: Processing event '{EventName}' with CorrelationId: {CorrelationId}", eventName, correlationId);

            return eventName switch
            {
                "TrainAndPublish" => await HandleTrainAndPublishAsync(correlationId),
                "ChangeCustomerData" => await HandleCustomerChangeAsync(correlationId),
                _ => new { Event = eventName, Status = "Unknown event" }
            };
        }
    }
}