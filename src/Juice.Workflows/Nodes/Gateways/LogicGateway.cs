namespace Juice.Workflows.Nodes.Gateways
{
    /// <summary>
    /// Routes workflow execution along one or more paths based on conditions evaluated
    /// against the current workflow context.
    ///
    /// Supports two routing modes (configured via <c>NodeContext.Properties["mode"]</c>):
    /// <list type="bullet">
    ///   <item><b>Exclusive</b> (default) — first matching condition activates its flow; remaining flows are skipped.</item>
    ///   <item><b>Inclusive</b> — all conditions are evaluated independently; every matching flow is activated.</item>
    /// </list>
    ///
    /// When the gateway has multiple incoming flows, it waits for all active incoming paths
    /// to deliver their tokens before evaluating conditions (converge-then-decide semantics),
    /// consistent with <see cref="InclusiveGateway"/>.
    ///
    /// Subclass this class and override <see cref="PreSelectOutgoingFlowAsync"/> to implement
    /// fully custom routing logic, or register a custom <see cref="ILogicConditionEvaluator"/>
    /// via DI to replace the built-in expression evaluator.
    /// </summary>
    public class LogicGateway : Gateway, ISelectiveGateway
    {
        protected readonly ILogicConditionEvaluator _evaluator;
        protected readonly ILogger _logger;

        public LogicGateway(
            ILogicConditionEvaluator evaluator,
            ILogger<LogicGateway> logger,
            IStringLocalizerFactory stringLocalizer)
            : base(stringLocalizer)
        {
            _evaluator = evaluator;
            _logger = logger;
        }

        public override LocalizedString DisplayText => Localizer["Logic Gateway"];

        /// <summary>
        /// Waits for all active incoming paths to converge, then proceeds.
        /// </summary>
        public override Task<NodeExecutionResult> StartAsync(
            WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            _logger.LogInformation("{NodeName} execute", node.Record.Name);

            if (flow == null)
            {
                return Task.FromResult(Fault("LogicGateway requires at least one incoming flow"));
            }

            if (workflowContext.AnyIncompleteActivePathTo(node))
            {
                return Task.FromResult(Noop("LogicGateway is waiting for all active incoming paths to complete"));
            }

            return Task.FromResult(Outcomes("Done"));
        }

        /// <summary>
        /// Evaluates the condition expression on the candidate outgoing flow.
        /// In exclusive mode, blocks all flows once one has already been selected.
        /// Returns null for unconditional flows (defers to SequenceFlow default handling).
        /// </summary>
        public override async Task<bool?> PreSelectOutgoingFlowAsync(
            WorkflowContext context, NodeContext source, NodeContext dest, FlowContext flow)
        {
            var mode = GetRoutingMode(source);

            if (mode == GatewayRoutingMode.Exclusive && context.AnyActiveFlowFrom(source))
            {
                // Another flow from this gateway is already active — skip remaining candidates
                return false;
            }

            if (string.IsNullOrEmpty(flow.Record.ConditionExpression))
            {
                // Unconditional flow — defer to SequenceFlow / default-flow handling
                return null;
            }

            return await _evaluator.EvaluateAsync(flow.Record.ConditionExpression, context, source);
        }

        /// <summary>
        /// Faults the workflow if no outgoing flow was activated after evaluation.
        /// </summary>
        public override Task<NodeExecutionResult?> PostExecuteCheckAsync(
            WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            _logger.LogInformation("{NodeName} post-execute check", node.Record.Name);

            if (!workflowContext.AnyActiveFlowFrom(node))
            {
                return Task.FromResult<NodeExecutionResult?>(
                    Fault($"No condition matched and no default flow is configured for Logic Gateway '{node.Record.Name}'. " +
                          "Add a default (unconditional) flow or ensure at least one condition evaluates to true."));
            }

            return base.PostExecuteCheckAsync(workflowContext, node, token);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GatewayRoutingMode GetRoutingMode(NodeContext node)
        {
            if (node.Properties != null
                && node.Properties.TryGetValue("mode", out var modeValue)
                && modeValue?.ToString()?.Equals("inclusive", StringComparison.OrdinalIgnoreCase) == true)
            {
                return GatewayRoutingMode.Inclusive;
            }
            return GatewayRoutingMode.Exclusive;
        }
    }
}
