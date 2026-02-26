namespace Juice.Workflows.Models
{
    public interface INode : IDisposable
    {
        LocalizedString DisplayText { get; }
        LocalizedString Category { get; }


        /// <summary>
        /// List of possible outcomes when the activity is executed.
        /// </summary>
        IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node);

        /// <summary>
        /// Executes the specified flow object.
        /// </summary>
        Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node,
            FlowContext? flow,
            CancellationToken token);

        /// <summary>
        /// Resume the specified activity.
        /// </summary>
        Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node,
            CancellationToken token);

    }

    public interface IEventNode : INode
    {
    }

    public interface IIntermediate : IEventNode
    {

    }

    public interface IThrowing : IEventNode
    {

    }

    public interface IBoundary : ICatching, IIntermediate
    {
        /// <summary>
        /// Check before start.
        /// </summary>
        Task<bool> PreStartCheckAsync(WorkflowContext workflowContext, NodeContext node, NodeContext ancestor,
            CancellationToken token);

        void NonInterupt();
    }

    public interface ICatching : IEventNode
    {

    }

    public interface IActivity : INode
    {

    }

    public interface IGateway : INode
    {
        /// <summary>
        /// Check after executed. Returns a fault result if the gateway state is invalid,
        /// or null to indicate the check passed.
        /// </summary>
        Task<NodeExecutionResult?> PostExecuteCheckAsync(WorkflowContext workflowContext, NodeContext node,
            CancellationToken token);

        /// <summary>
        /// Called when this gateway is the SOURCE of a candidate outgoing flow.
        /// Returns true to allow, false to block, or null to defer to standard condition matching.
        /// </summary>
        Task<bool?> PreSelectOutgoingFlowAsync(WorkflowContext context, NodeContext source,
            NodeContext dest, FlowContext flow);

        /// <summary>
        /// Called when this gateway is the DESTINATION of a candidate incoming flow.
        /// Returns true to allow, false to block, or null to defer to standard condition matching.
        /// </summary>
        Task<bool?> PreSelectIncomingFlowAsync(WorkflowContext context, NodeContext source,
            NodeContext dest, FlowContext flow);
    }

    public interface IExclusive : IGateway { }
    public interface IEventBased : IGateway { }
}
