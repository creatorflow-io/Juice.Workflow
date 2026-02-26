namespace Juice.Workflows.Nodes.Gateways
{
    public class EventBasedGateway : Gateway, IExclusive, IEventBased
    {
        private ILogger _logger;
        public EventBasedGateway(ILogger<EventBasedGateway> logger,
            IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
            _logger = logger;
        }

        public override LocalizedString DisplayText => Localizer["Event-based Gateway"];


        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " execute");
            if (flow == null)
            {
                return Fault("EventBasedGateway required single incoming flow");
            }
            if (workflowContext.AnyActiveFlowTo(node, flow.Record.Id))
            {
                return Fault("EventBasedGateway must has single active incoming flow");
            }

            return SourceOutcomes(workflowContext, flow);
        }

        /// <summary>
        /// All outgoing flows are activated (each connects to an intermediate catch event).
        /// Throws for invalid workflow structure: targets must be intermediate catching events.
        /// </summary>
        public override Task<bool?> PreSelectOutgoingFlowAsync(WorkflowContext context, NodeContext source,
            NodeContext dest, FlowContext flow)
        {
            if (!(dest.Node is IIntermediate && dest.Node is ICatching))
                throw new InvalidOperationException("The nodes next to EventBasedGateway must be intermediate catching event");
            return Task.FromResult<bool?>(true);
        }

        /// <summary>
        /// Only one token may arrive at a time (exclusive convergence).
        /// </summary>
        public override Task<bool?> PreSelectIncomingFlowAsync(WorkflowContext context, NodeContext source,
            NodeContext dest, FlowContext flow)
        {
            if (context.AnyActiveFlowTo(dest, default))
                return Task.FromResult<bool?>(false);
            return Task.FromResult<bool?>(null);
        }

        public override Task<NodeExecutionResult?> PostExecuteCheckAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " post check");
            if (!workflowContext.AnyActiveFlowFrom(node))
            {
                return Task.FromResult<NodeExecutionResult?>(Fault("No sequence flow can be selected. To ensure a sequence flow will always be selected, have no condition on one of your flows"));
            }

            return base.PostExecuteCheckAsync(workflowContext, node, token);
        }

    }
}
