namespace Juice.Workflows.Designer.Commands
{
    public record RenameWorkflowDefinitionCommand(
        string Id,
        string NewName
    ) : MessageBase, IRequest<IOperationResult>;
}
