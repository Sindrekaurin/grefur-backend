using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using grefurBackend.Events;

namespace grefurBackend.Infrastructure
{
    /// <summary>
    /// Simple in-memory EventBus implementation.
    /// Thread-safe, supports polymorphic dispatch (handlers registered for base event types
    /// will receive derived events).
    /// </summary>
    public class EventBus
    {
        private readonly ConcurrentDictionary<Type, ConcurrentBag<object>> _handlers = new();

        /// <summary>
        /// Publishes an event to all subscribers.
        /// </summary>
        public async Task Publish(Event DomainEvent)
        {
            if (DomainEvent == null) return;

            var EventType = DomainEvent.GetType();

            foreach (var Kvp in _handlers)
            {
                var RegisteredType = Kvp.Key;
                if (!RegisteredType.IsAssignableFrom(EventType)) continue;

                foreach (var Handler in Kvp.Value.ToArray())
                {
                    try
                    {
                        var HandlerTask = (Task)((dynamic)Handler).Handle((dynamic)DomainEvent);
                        await HandlerTask.ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignoring exceptions for now
                    }
                }
            }
        }

        /// <summary>
        /// Subscribe to events of type TEvent.
        /// </summary>
        public void Subscribe<TEvent>(IEventHandler<TEvent> Handler) where TEvent : Event
        {
            if (Handler == null) throw new ArgumentNullException(nameof(Handler));

            var Bag = _handlers.GetOrAdd(typeof(TEvent), _ => new ConcurrentBag<object>());
            Bag.Add(Handler);
        }

        /// <summary>
        /// Remove a handler subscription for events of type TEvent.
        /// </summary>
        public void Unsubscribe<TEvent>(IEventHandler<TEvent> Handler) where TEvent : Event
        {
            if (Handler == null) throw new ArgumentNullException(nameof(Handler));

            if (_handlers.TryGetValue(typeof(TEvent), out var Bag))
            {
                var NewBag = new ConcurrentBag<object>(Bag.Except(new[] { Handler }));
                _handlers[typeof(TEvent)] = NewBag;
            }
        }

        public void Unsubscribe<TEvent>(Func<TEvent, Task> Handler) where TEvent : Event
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var Bag))
            {
                var NewBag = new ConcurrentBag<object>();
                foreach (var H in Bag)
                {
                    if (!ReferenceEquals(H, Handler))
                        NewBag.Add(H);
                }
                _handlers[typeof(TEvent)] = NewBag;
            }
        }

        /// <summary>
        /// Syntax for temporary handlers: publish event and wait for response.
        /// </summary>
        public async Task<TResult?> RequestAsync<TRequest, TResult>(
            TRequest RequestEvent,
            Func<TResult, bool> Filter,
            int TimeoutMs = 5000)
            where TRequest : Event
            where TResult : Event
        {
            var Tcs = new TaskCompletionSource<TResult?>();

            TempEventHandler<TResult>? TempHandler = null;

            // Dynamic temporary handler
            TempHandler = new TempEventHandler<TResult>(async Response =>
            {
                if (Filter(Response))
                {
                    Tcs.TrySetResult(Response);
                    Unsubscribe(TempHandler);
                }
                await Task.CompletedTask;
            });

            Subscribe(TempHandler);

            await Publish(RequestEvent).ConfigureAwait(false);

            var DelayTask = Task.Delay(TimeoutMs);
            var CompletedTask = await Task.WhenAny(Tcs.Task, DelayTask).ConfigureAwait(false);

            return CompletedTask == Tcs.Task ? Tcs.Task.Result : default;
        }

        /// <summary>
        /// Simple dynamic handler for temporary requests.
        /// </summary>
        private class TempEventHandler<TEvent> : IEventHandler<TEvent> where TEvent : Event
        {
            private readonly Func<TEvent, Task> _handleFunc;
            public TempEventHandler(Func<TEvent, Task> HandleFunc) => _handleFunc = HandleFunc;
            public Task Handle(TEvent Evt) => _handleFunc(Evt);
        }
    }
}