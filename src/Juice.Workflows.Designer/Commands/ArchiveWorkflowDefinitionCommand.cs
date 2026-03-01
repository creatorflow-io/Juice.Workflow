namespace Juice.Workflows.Designer.Commands
{
    public record ArchiveWorkflowDefinitionCommand(string Id) : MessageBase, IRequest<IOperationResult>;
}
