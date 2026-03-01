namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public interface IDefinitionRepository
    {
        Task<IOperationResult> CreateAsync(WorkflowDefinition workflowDefinition, CancellationToken token);
        Task<IOperationResult> UpdateAsync(WorkflowDefinition workflowDefinition, CancellationToken token);
        Task<WorkflowDefinition?> GetAsync(string definitionId, CancellationToken token);
        Task<bool> ExistAsync(string definitionId, CancellationToken token);
        Task<IOperationResult> DeleteAsync(string definitionId, CancellationToken token);

        /// <summary>Returns lightweight summary list ordered by ModifiedAt desc. Filter by status if provided.</summary>
        Task<IEnumerable<WorkflowDefinitionSummary>> ListAsync(WorkflowDefinitionStatus? status, CancellationToken token);

        /// <summary>Returns true if a definition with the given name (case-insensitive) already exists,
        /// optionally excluding the definition with <paramref name="excludeId"/> (used for rename validation).</summary>
        Task<bool> ExistsByNameAsync(string name, string? excludeId, CancellationToken token);
    }
}
