namespace Juice.Workflows.EF.Repositories
{
    internal class WorkflowRepository<TContext> : IWorkflowRepository
        where TContext : DbContext
    {
        private readonly TContext _dbContext;
        public virtual IQueryable<WorkflowRecord> WorkflowRecords => _dbContext.Set<WorkflowRecord>();

        public WorkflowRepository(TContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IOperationResult> CreateAsync(WorkflowRecord workflow, CancellationToken token)
        {
            try
            {
                if (await WorkflowRecords.AnyAsync(d => d.Id == workflow.Id))
                {
                    return OperationResult.Failed("Workflow is already exists.");
                }
                _dbContext.Add(workflow);
                await _dbContext.SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
        public Task<WorkflowRecord?> GetAsync(string workflowId, CancellationToken token)
            => WorkflowRecords.FirstOrDefaultAsync(d => d.Id == workflowId, token);
        public async Task<IOperationResult> UpdateAsync(WorkflowRecord workflow, CancellationToken token)
        {
            try
            {
                if (!await WorkflowRecords.AnyAsync(d => d.Id == workflow.Id))
                {
                    return OperationResult.Failed("Workflow not found.");
                }
                _dbContext.Update(workflow);
                await _dbContext.SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }
}
