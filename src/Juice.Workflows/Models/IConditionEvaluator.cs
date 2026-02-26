namespace Juice.Workflows.Models
{
    /// <summary>
    /// Evaluates a condition expression against the current workflow execution context.
    /// Implementations can support simple outcome-name matching, scripting languages,
    /// or any other evaluation strategy.
    /// </summary>
    public interface IConditionEvaluator
    {
        Task<bool> EvaluateAsync(string expression, WorkflowContext context, NodeContext source);
    }

    /// <summary>
    /// Default evaluator: a condition passes when the expression string matches one of the
    /// outcome names produced by the source node. This preserves the original behavior.
    /// </summary>
    public class OutcomeConditionEvaluator : IConditionEvaluator
    {
        public static readonly OutcomeConditionEvaluator Default = new();

        public Task<bool> EvaluateAsync(string expression, WorkflowContext context, NodeContext source)
            => Task.FromResult(context.GetOutcomes(source.Record.Id).Contains(expression));
    }
}
