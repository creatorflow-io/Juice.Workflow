namespace Juice.Workflows.Models
{
    /// <summary>
    /// Evaluates condition expressions for <see cref="Juice.Workflows.Nodes.Gateways.LogicGateway"/>.
    /// Registered independently from <see cref="IConditionEvaluator"/> so that
    /// SequenceFlow's default outcome-name evaluator and LogicGateway's expression
    /// evaluator can each have their own default implementation without keyed DI.
    /// </summary>
    public interface ILogicConditionEvaluator : IConditionEvaluator
    {
    }
}
