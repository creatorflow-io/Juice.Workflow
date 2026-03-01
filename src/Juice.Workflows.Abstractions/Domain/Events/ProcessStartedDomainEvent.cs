
namespace Juice.Workflows.Domain.Events
{
    public record ProcessStartedDomainEvent : MessageBase, INotification
    {
        public NodeRecord Node { get; init; }
        public ProcessStartedDomainEvent(NodeRecord node)
        {
            Node = node;
        }
    }
}
