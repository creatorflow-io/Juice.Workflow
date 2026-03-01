namespace Juice.Workflows.Designer.Commands
{
    public class RenameWorkflowDefinitionCommandHandler
        : IRequestHandler<RenameWorkflowDefinitionCommand, IOperationResult>
    {
        private readonly IDefinitionRepository _repository;

        public RenameWorkflowDefinitionCommandHandler(IDefinitionRepository repository)
        {
            _repository = repository;
        }

        public async ValueTask<IOperationResult> Handle(
            RenameWorkflowDefinitionCommand request, CancellationToken cancellationToken)
        {
            var definition = await _repository.GetAsync(request.Id, cancellationToken);
            if (definition == null)
            {
                return OperationResult.Failed($"Workflow definition '{request.Id}' not found.");
            }

            var nameConflict = await _repository.ExistsByNameAsync(
                request.NewName, request.Id, cancellationToken);
            if (nameConflict)
            {
                return OperationResult.Failed(
                    $"A workflow definition named '{request.NewName}' already exists.");
            }

            definition.Rename(request.NewName);
            return await _repository.UpdateAsync(definition, cancellationToken);
        }
    }
}
