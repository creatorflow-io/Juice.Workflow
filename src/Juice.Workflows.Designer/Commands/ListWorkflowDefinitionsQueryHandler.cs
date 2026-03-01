namespace Juice.Workflows.Designer.Commands
{
    public class ListWorkflowDefinitionsQueryHandler
        : IRequestHandler<ListWorkflowDefinitionsQuery, IEnumerable<WorkflowDefinitionSummary>>
    {
        private readonly IDefinitionRepository _repository;

        public ListWorkflowDefinitionsQueryHandler(IDefinitionRepository repository)
        {
            _repository = repository;
        }

        public async ValueTask<IEnumerable<WorkflowDefinitionSummary>> Handle(
            ListWorkflowDefinitionsQuery request, CancellationToken cancellationToken)
        {
            return await _repository.ListAsync(request.Status, cancellationToken);
        }
    }
}
