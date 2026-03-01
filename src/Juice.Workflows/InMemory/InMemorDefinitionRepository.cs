namespace Juice.Workflows.InMemory
{
    internal class InMemorDefinitionRepository : IDefinitionRepository
    {
        private IDictionary<string, WorkflowDefinition> _definitions = new Dictionary<string, WorkflowDefinition>();
        public InMemorDefinitionRepository()
        {

        }
        public Task<IOperationResult> CreateAsync(WorkflowDefinition workflowDefinition, CancellationToken token)
        {
            _definitions[workflowDefinition.Id] = workflowDefinition;
            return Task.FromResult(OperationResult.Success);
        }

        public Task<IOperationResult> DeleteAsync(string definitionId, CancellationToken token)
        {
            if (_definitions.ContainsKey(definitionId))
            {
                _definitions.Remove(definitionId);
            }
            return Task.FromResult(OperationResult.Success);
        }
        public Task<bool> ExistAsync(string definitionId, CancellationToken token)
            => Task.FromResult(_definitions.ContainsKey(definitionId));
        public Task<WorkflowDefinition?> GetAsync(string definitionId, CancellationToken token)
            => Task.FromResult(_definitions.ContainsKey(definitionId) ? _definitions[definitionId] : default);
        public Task<IOperationResult> UpdateAsync(WorkflowDefinition workflowDefinition, CancellationToken token)
        {
            _definitions[workflowDefinition.Id] = workflowDefinition;
            return Task.FromResult(OperationResult.Success);
        }

        public Task<IEnumerable<WorkflowDefinitionSummary>> ListAsync(WorkflowDefinitionStatus? status, CancellationToken token)
        {
            var query = _definitions.Values.AsEnumerable();
            if (status.HasValue)
            {
                query = query.Where(d => d.Status == status.Value);
            }
            var summaries = query
                .OrderByDescending(d => d.ModifiedDate)
                .Select(d => new WorkflowDefinitionSummary(d.Id, d.Name, d.RawFormat, d.Status, d.ModifiedDate));
            return Task.FromResult(summaries);
        }

        public Task<bool> ExistsByNameAsync(string name, string? excludeId, CancellationToken token)
        {
            var exists = _definitions.Values.Any(d =>
                string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)
                && (excludeId == null || d.Id != excludeId));
            return Task.FromResult(exists);
        }
    }
}
