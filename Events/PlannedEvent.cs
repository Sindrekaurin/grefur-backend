namespace grefurBackend.Events;

/* Summary of record: Central definition for scheduled events to avoid CS0101 and CS0246 errors. */
public record PlannedEvent(string Id, DateTime PublishTime, Event EventPayload);