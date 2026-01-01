using System.Threading.Tasks;
using grefurBackend.Events;

namespace grefurBackend.Infrastructure;

public interface IEventHandler<in TEvent> where TEvent : Event
{
    Task Handle(TEvent domainEvent);
}