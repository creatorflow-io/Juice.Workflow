namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public record WorkflowDefinitionSummary(
        string Id,
        string Name,
        string? RawFormat,
        WorkflowDefinitionStatus Status,
        DateTimeOffset? ModifiedAt);
}
