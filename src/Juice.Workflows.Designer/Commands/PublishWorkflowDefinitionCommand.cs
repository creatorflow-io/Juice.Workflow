namespace Juice.Workflows.Designer.Commands
{
    public record PublishWorkflowDefinitionCommand(string Id) : MessageBase, IRequest<IOperationResult>;
}
