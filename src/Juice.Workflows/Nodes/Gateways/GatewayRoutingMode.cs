namespace Juice.Workflows.Nodes.Gateways
{
    /// <summary>
    /// Controls how a <see cref="LogicGateway"/> selects outgoing flows.
    /// </summary>
    public enum GatewayRoutingMode
    {
        /// <summary>
        /// First matching condition activates its flow; remaining flows are skipped.
        /// </summary>
        Exclusive = 0,

        /// <summary>
        /// All conditions are evaluated independently; every matching flow is activated.
        /// </summary>
        Inclusive = 1
    }
}
