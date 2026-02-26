namespace Juice.Workflows.Models
{
    public class SequenceFlow : IFlow
    {
        private readonly IConditionEvaluator _evaluator;

        public SequenceFlow() : this(OutcomeConditionEvaluator.Default) { }

        public SequenceFlow(IConditionEvaluator evaluator)
        {
            _evaluator = evaluator;
        }

        public async Task<bool> PreSelectCheckAsync(WorkflowContext context, NodeContext source,
            NodeContext dest, FlowContext flow)
        {
            await Task.Yield();

            // Default flows are always selected regardless of conditions.
            if (context.IsDefaultOutgoing(flow, source))
                return true;

            // Let the source gateway decide whether this outgoing flow should be activated.
            if (source.Node is IGateway sourceGateway)
            {
                var result = await sourceGateway.PreSelectOutgoingFlowAsync(context, source, dest, flow);
                if (result.HasValue)
                    return result.Value;
                // null = no opinion from the gateway; fall through to condition matching below.
            }

            // Let the destination gateway decide whether this incoming flow is acceptable.
            if (dest.Node is IGateway destGateway)
            {
                var result = await destGateway.PreSelectIncomingFlowAsync(context, source, dest, flow);
                if (result.HasValue)
                    return result.Value;
            }

            // Unconditional flow: always selected.
            if (flow.Record.ConditionExpression == null)
                return true;

            // Evaluate the condition expression against the source node's current context.
            return await _evaluator.EvaluateAsync(flow.Record.ConditionExpression, context, source);
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects).

                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
