namespace Juice.Workflows
{
    public interface IWorkflow
    {
        WorkflowContext? ExecutedContext { get; }
        Task<IOperationResult<WorkflowExecutionResult>> StartAsync(string workflowId,
            string? correlationId, string? name,
            Dictionary<string, object?>? parameters,
            CancellationToken token = default);
        Task<IOperationResult<WorkflowExecutionResult>> ResumeAsync(string workflowId,
            string nodeId,
            Dictionary<string, object?>? parameters,
            CancellationToken token = default);

    }

}
