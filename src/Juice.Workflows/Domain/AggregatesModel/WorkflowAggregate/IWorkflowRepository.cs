namespace Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate
{
    public interface IWorkflowRepository
    {
        Task<IOperationResult> CreateAsync(WorkflowRecord workflow, CancellationToken token);
        Task<IOperationResult> UpdateAsync(WorkflowRecord workflow, CancellationToken token);
        Task<WorkflowRecord?> GetAsync(string workflowId, CancellationToken token);
    }
}
