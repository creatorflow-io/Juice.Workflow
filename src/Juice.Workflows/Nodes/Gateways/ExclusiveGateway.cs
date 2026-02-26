namespace Juice.Workflows.Nodes.Gateways
{
    public class ExclusiveGateway : Gateway, IExclusive
    {
        private ILogger _logger;
        public ExclusiveGateway(ILogger<ExclusiveGateway> logger, IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
            _logger = logger;
        }

        public override LocalizedString DisplayText => Localizer["Exclusive Gateway"];

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " execute");
            if (flow == null)
            {
                return Fault("ExclusiveGateway required single incoming flow");
            }
            if (workflowContext.AnyActiveFlowTo(node, flow.Record.Id))
            {
                return Fault("ExclusiveGateway must has single active incoming flow");
            }

            return SourceOutcomes(workflowContext, flow);
        }


        /// <summary>
        /// Block outgoing flow selection if a flow from this gateway is already active
        /// (only one branch may be taken at a time).
        /// </summary>
        public override Task<bool?> PreSelectOutgoingFlowAsync(WorkflowContext context, NodeContext source,
            NodeContext dest, FlowContext flow)
        {
            if (context.AnyActiveFlowFrom(source))
                return Task.FromResult<bool?>(false);
            return Task.FromResult<bool?>(null);
        }

        /// <summary>
        /// Block incoming flow if another flow is already active toward this gateway
        /// (exclusive convergence: only one token may arrive).
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
