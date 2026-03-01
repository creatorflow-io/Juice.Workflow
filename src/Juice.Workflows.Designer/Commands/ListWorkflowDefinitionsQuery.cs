namespace Juice.Workflows.Designer.Commands
{
    public record ListWorkflowDefinitionsQuery(
        WorkflowDefinitionStatus? Status = null
    ) : MessageBase, IRequest<IEnumerable<WorkflowDefinitionSummary>>;
}
