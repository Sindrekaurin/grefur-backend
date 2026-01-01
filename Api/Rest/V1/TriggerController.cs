using Microsoft.AspNetCore.Mvc;   // for ControllerBase, ApiController, Route, HttpGet, FromQuery
using Microsoft.Extensions.Logging; // for ILogger
using System.Threading.Tasks;       // for Task
using System.Collections.Generic;   // for List<T>
using grefurBackend.Events;         // for TrainAndPublishEvent
using grefurBackend.Services;       // for LoggerService
using grefurBackend.Events.Domain;  // for ValueReceivedEvent
using grefurBackend.Services;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using Microsoft.Extensions.Logging;
using grefurBackend.Events.Queries;
using grefurBackend.Events;
using grefurBackend.Models.AlarmConfiguration;


namespace grefurBackend.Api.Rest.V1
{
	[ApiController]
	[Route("v1/[controller]")]
	public class TriggerController : ControllerBase
	{
		private readonly EventBus _eventBus;
		private readonly LoggerService _loggerService;
		private readonly ILogger<TriggerController> _logger;

		public TriggerController(EventBus eventBus, LoggerService loggerService, ILogger<TriggerController> logger)
		{
			_eventBus = eventBus;
			_loggerService = loggerService;
			_logger = logger;

		}

        // GET /v1/trigger?events=TrainAndPublish
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string events)
        {
            if (string.IsNullOrWhiteSpace(events))
                return BadRequest("No events specified");

            var eventList = events.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var responses = new List<object>();

            foreach (var evt in eventList)
            {
                var trimmedEvent = evt.Trim();

                switch (trimmedEvent)
                {
                    case "TrainAndPublish":
                        var dummyConfig = new MlAlarmConfiguration
                        {
                            CustomerId = "CUST-001",
                            TargetMeasurementId = "Grefur_3461/900/320/001/RT401/value",
                            FeatureMeasurementIds = new List<string>
                            {
                                "Grefur_235cfe/900/320/001/RT901/value"
                            },
                            TrainingFrequency = TrainingFrequency.Weekly,
                            SampleIntervalMinutes = 5
                        };



                        var correlationId = Guid.NewGuid().ToString();
                        var source = "TriggerController";
                        var trainEvent = new TrainAndPublishEvent(dummyConfig, source, correlationId);

                        _logger.LogInformation(
                            "Triggering TrainAndPublish for customer {CustomerId} with CorrelationId {CorrelationId}",
                            dummyConfig.CustomerId,
                            correlationId
                        );

                        // Publisering til bussen
                        await _eventBus.Publish(trainEvent);

                        responses.Add(new
                        {
                            Event = trimmedEvent,
                            CustomerId = dummyConfig.CustomerId,
                            CorrelationId = correlationId,
                            Status = "Triggered"
                        });
                        break;

                    default:
                        responses.Add(new
                        {
                            Event = evt,
                            Status = "Unknown event"
                        });
                        break;
                }
            }

            return Ok(responses);
        }


    }
}
