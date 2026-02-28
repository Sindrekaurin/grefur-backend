using System;
using System.Threading.Tasks;
using grefurBackend.Events;

namespace grefurBackend.Infrastructure
{
    public class InlineEventHandler<T> : IEventHandler<T> where T : Event
    {
        private readonly Func<T, Task> _action;

        public InlineEventHandler(Func<T, Task> Action)
        {
            _action = Action;
        }

        public async Task Handle(T Evt)
        {
            await _action(Evt);
        }
    }
}