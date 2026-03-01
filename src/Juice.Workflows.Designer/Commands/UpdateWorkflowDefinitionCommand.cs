namespace Juice.Workflows.Designer.Commands
{
    public record UpdateWorkflowDefinitionCommand(
        string Id,
        string RawData,
        string RawFormat
    ) : MessageBase, IRequest<IOperationResult>;
}
