namespace Juice.Workflows.Designer.Commands
{
    public record CreateWorkflowDefinitionCommand(
        string Name,
        string RawData,
        string RawFormat
    ) : MessageBase, IRequest<IOperationResult<string>>;
}
