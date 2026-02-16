
namespace Juice.Workflows.Domain.Events
{
    public record class ServiceTaskCompleteDomainEvent : MessageBase, INotification
    {
        public NodeContext Node { get; init; }

        public ServiceTaskCompleteDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
