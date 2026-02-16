namespace Juice.Workflows.Domain.Commands
{
    public record InitWorkflowStartEventCommand : MessageBase, IRequest<IOperationResult>
    {
        public string WorkflowId { get; init; }
        public NodeRecord[] StartNodes { get; init; }

        public InitWorkflowStartEventCommand(string workflowId, NodeRecord[] startNodes)
        {
            WorkflowId = workflowId;
            StartNodes = startNodes;
        }
    }
}
