namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public enum WorkflowDefinitionStatus
    {
        /// <summary>Saved but not yet live; cannot be used to start new workflow instances.</summary>
        Draft = 0,
        /// <summary>Published; available for execution. Promoted from Draft via an explicit Publish action.</summary>
        Active = 1,
        /// <summary>Retired; no new executions can be started. Promoted from Active via an explicit Archive action.</summary>
        Archived = 2
    }
}
