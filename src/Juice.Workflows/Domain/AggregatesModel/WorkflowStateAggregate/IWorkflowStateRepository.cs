namespace Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate
{
    public interface IWorkflowStateRepository
    {
        Task<IOperationResult> PersistAsync(string workflowId, WorkflowState state, CancellationToken token);

        Task<WorkflowState> GetAsync(string workflowId, CancellationToken token);
    }
}
