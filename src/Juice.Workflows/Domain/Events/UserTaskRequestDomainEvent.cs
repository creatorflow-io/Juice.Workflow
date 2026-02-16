
namespace Juice.Workflows.Domain.Events
{
    public record UserTaskRequestDomainEvent : MessageBase, INotification
    {
        public NodeContext Node { get; init; }

        public UserTaskRequestDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
