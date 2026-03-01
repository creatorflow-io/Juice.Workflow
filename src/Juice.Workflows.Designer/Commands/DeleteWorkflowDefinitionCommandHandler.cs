namespace Juice.Workflows.Designer.Commands
{
    public class DeleteWorkflowDefinitionCommandHandler
        : IRequestHandler<DeleteWorkflowDefinitionCommand, IOperationResult>
    {
        private readonly IDefinitionRepository _repository;

        public DeleteWorkflowDefinitionCommandHandler(IDefinitionRepository repository)
        {
            _repository = repository;
        }

        public async ValueTask<IOperationResult> Handle(
            DeleteWorkflowDefinitionCommand request, CancellationToken cancellationToken)
        {
            var exists = await _repository.ExistAsync(request.Id, cancellationToken);
            if (!exists)
            {
                return OperationResult.Failed($"Workflow definition '{request.Id}' not found.");
            }
            return await _repository.DeleteAsync(request.Id, cancellationToken);
        }
    }
}
