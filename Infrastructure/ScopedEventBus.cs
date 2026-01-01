using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using grefurBackend.Events;

namespace grefurBackend.Infrastructure;

public class ScopedEventBus : EventBus
{
    private readonly ConcurrentDictionary<Type, List<object>> handlers = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> scopes = new();

    public new void subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : Event
    {
        var eventType = typeof(TEvent);

        handlers.AddOrUpdate(
            eventType,
            _ => new List<object> { handler },
            (_, list) =>
            {
                list.Add(handler);
                return list;
            });
    }

    public new async Task publish(Event domainEvent)
    {
        var scopeKey = extractScope(domainEvent);
        var semaphore = scopes.GetOrAdd(scopeKey, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            await dispatch(domainEvent).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task dispatch(Event domainEvent)
    {
        var eventType = domainEvent.GetType();

        if (!handlers.TryGetValue(eventType, out var registeredHandlers))
        {
            return;
        }

        foreach (var handler in registeredHandlers)
        {
            try
            {
                var task = (Task)((dynamic)handler).Handle((dynamic)domainEvent);
                await task.ConfigureAwait(false);
            }
            catch
            {
                // swallow per-handler exceptions
            }
        }
    }

    private string extractScope(Event domainEvent)
    {
        var property = domainEvent.GetType().GetProperty("customerId");

        if (property == null)
        {
            return "global";
        }

        return property.GetValue(domainEvent)?.ToString() ?? "global";
    }
}