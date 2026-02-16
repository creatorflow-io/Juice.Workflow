
namespace Juice.Workflows.Domain.Events
{
    public record ProcessStartedDomainEvent : MessageBase, INotification
    {
        public NodeContext Node { get; init; }
        public ProcessStartedDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
