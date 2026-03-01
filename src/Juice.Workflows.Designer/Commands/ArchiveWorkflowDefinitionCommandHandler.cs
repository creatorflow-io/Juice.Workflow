namespace Juice.Workflows.Designer.Commands
{
    public class ArchiveWorkflowDefinitionCommandHandler
        : IRequestHandler<ArchiveWorkflowDefinitionCommand, IOperationResult>
    {
        private readonly IDefinitionRepository _repository;

        public ArchiveWorkflowDefinitionCommandHandler(IDefinitionRepository repository)
        {
            _repository = repository;
        }

        public async ValueTask<IOperationResult> Handle(
            ArchiveWorkflowDefinitionCommand request, CancellationToken cancellationToken)
        {
            var definition = await _repository.GetAsync(request.Id, cancellationToken);
            if (definition == null)
            {
                return OperationResult.Failed($"Workflow definition '{request.Id}' not found.");
            }

            try
            {
                definition.Archive();
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult.Failed(ex);
            }

            return await _repository.UpdateAsync(definition, cancellationToken);
        }
    }
}
