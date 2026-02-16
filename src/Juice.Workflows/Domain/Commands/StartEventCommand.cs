using Juice.Workflows.Nodes;

namespace Juice.Workflows.Domain.Commands
{
    public record StartEventCommand<TEvent> : MessageBase, INodeCommand, IRequest<IOperationResult>
        where TEvent : Event
    {
        public string WorkflowId { get; init; }
        public string? CorrelationId { get; init; }
        public NodeContext Node { get; init; }

        public StartEventCommand(string workflowId, string? correlationId, NodeContext node)
        {
            WorkflowId = workflowId;
            CorrelationId = correlationId;
            Node = node;
        }
    }
}
