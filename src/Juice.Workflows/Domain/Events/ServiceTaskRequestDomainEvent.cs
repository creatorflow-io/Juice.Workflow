
namespace Juice.Workflows.Domain.Events
{
    public record ServiceTaskRequestDomainEvent : MessageBase, INotification
    {
        public NodeContext Node { get; init; }

        public ServiceTaskRequestDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
