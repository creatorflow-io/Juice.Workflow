namespace Juice.Workflows.Designer.Commands
{
    public record DeleteWorkflowDefinitionCommand(string Id) : MessageBase, IRequest<IOperationResult>;
}
