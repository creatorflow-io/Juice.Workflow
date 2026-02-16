
namespace Juice.Workflows.Domain.Events
{
    public record UserTaskCompleteDomainEvent : MessageBase, INotification
    {
        public NodeContext Node { get; init; }

        public UserTaskCompleteDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
