namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public interface IDefinitionRepository
    {
        Task<IOperationResult> CreateAsync(WorkflowDefinition workflowDefinition, CancellationToken token);
        Task<IOperationResult> UpdateAsync(WorkflowDefinition workflowDefinition, CancellationToken token);
        Task<WorkflowDefinition?> GetAsync(string definitionId, CancellationToken token);
        Task<bool> ExistAsync(string definitionId, CancellationToken token);
        Task<IOperationResult> DeleteAsync(string definitionId, CancellationToken token);
    }
}
