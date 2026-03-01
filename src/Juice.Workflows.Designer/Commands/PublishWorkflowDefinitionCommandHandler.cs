namespace Juice.Workflows.Designer.Commands
{
    public class PublishWorkflowDefinitionCommandHandler
        : IRequestHandler<PublishWorkflowDefinitionCommand, IOperationResult>
    {
        private readonly IDefinitionRepository _repository;

        public PublishWorkflowDefinitionCommandHandler(IDefinitionRepository repository)
        {
            _repository = repository;
        }

        public async ValueTask<IOperationResult> Handle(
            PublishWorkflowDefinitionCommand request, CancellationToken cancellationToken)
        {
            var definition = await _repository.GetAsync(request.Id, cancellationToken);
            if (definition == null)
            {
                return OperationResult.Failed($"Workflow definition '{request.Id}' not found.");
            }

            try
            {
                definition.Publish();
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult.Failed(ex);
            }

            return await _repository.UpdateAsync(definition, cancellationToken);
        }
    }
}
