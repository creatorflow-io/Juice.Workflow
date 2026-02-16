namespace Juice.Workflows.Domain.Events
{
    public record TimerEventStartDomainEvent : MessageBase, INotification
    {
        public string WorkflowId { get; init; }
        public NodeContext Node { get; init; }
        public TimerEventStartDomainEvent(string workflowId, NodeContext node)
        {
            WorkflowId = workflowId;
            Node = node;
        }
    }
}
